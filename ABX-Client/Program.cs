using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

// Add your ABX server host url and port here
const string HOST = "localhost";
const int PORT = 3000;
const int READ_TIMEOUT_MS = 300;
const int PACKET_SIZE = 17;

const int MAX_RETRYS = 3;
const int BASE_RETRY_DELAY = 500;

List<Packet> packets = [];

try
{
    // Creating a cancellation token with a timeout.
    var cts = new CancellationTokenSource();
    cts.CancelAfter(TimeSpan.FromMilliseconds(READ_TIMEOUT_MS));
    CancellationToken cancellationToken = cts.Token;

    await GetAllPacketsAsync(cancellationToken);
    
    if(packets.Count == 0)
    {
        Console.WriteLine("No packets received.");
        return;
    }
    
    var missingPacketSequenceNumbers = GetMissingPacketSequenceIds();
    if (missingPacketSequenceNumbers.Count > 255)
    {
        throw new Exception("Sequence number must be between 0 and 255");
    }

    foreach (var sequenceNumber in missingPacketSequenceNumbers)
    {
        if (sequenceNumber > 255)
        {
            throw new Exception($"Invalid sequence number: {sequenceNumber}. Must be <= 255.");
        }

        Console.WriteLine($"Requesting missing packet with sequence id: {sequenceNumber}");
        await GetMissingPacketsAsync(cancellationToken, sequenceNumber);

    }
    packets = [.. packets.OrderBy(x => x.Sequence)];
    Console.WriteLine("Finished processing packets.");

    var JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    var json = JsonSerializer.Serialize(packets, JsonOptions);
    Console.WriteLine($"Serialized packets to JSON: \n {json}");
    
    var currentDirectory = Directory.GetCurrentDirectory();
    var filePath = Path.Combine(currentDirectory, "result.json");
    await File.WriteAllTextAsync(filePath, json, cancellationToken);
    Console.WriteLine($"Saved packets to {filePath}");

}
catch (Exception ex)
{
    Console.WriteLine($"ABX client failed with error: {ex.Message}");
    return;
}

// Sends a get all request to the server.
async Task GetAllPacketsAsync(CancellationToken cancellationToken)
{
    await RetryAsync(async () =>
    {
        Console.WriteLine("Connecting to server...");

        using var client = new TcpClient();
        await client.ConnectAsync(HOST, PORT, cancellationToken);
        
        if (!client.Connected)
        {
            Console.WriteLine("Failed to connect to server. Please check the host and port and see if the server is running.");
            return;
        }
        Console.WriteLine($"Connected to server at {HOST}:{PORT}");

        using var stream = client.GetStream();
        byte[] requestPayload = [(byte)CallType.StreamAllPackets, 0x00];
        await stream.WriteAsync(requestPayload, cancellationToken);

        await ReceivePacketsAsync(stream, cancellationToken);

        Console.WriteLine("Server closed connection after streaming all packets.");

    }, "Requesting all packets", MAX_RETRYS, BASE_RETRY_DELAY, cancellationToken);
}

// Gets the missing packet based on sequence number.
async Task GetMissingPacketsAsync(CancellationToken cancellationToken, int sequenceNumber)
{
    await RetryAsync(async () =>
    {
        Console.WriteLine("Connecting to server...");

        using var client = new TcpClient();
        await client.ConnectAsync(HOST, PORT, cancellationToken);

        if (!client.Connected)
        {
            Console.WriteLine("Failed to connect to server. Please check the host and port and see if the server is running.");
            return;
        }
        Console.WriteLine($"Connected to server at {HOST}:{PORT}");

        using var stream = client.GetStream();
        byte[] requestPayload = [(byte)CallType.ResendPackets, (byte)sequenceNumber];
        
        await stream.WriteAsync(requestPayload, cancellationToken);
        await ReceivePacketsAsync(stream, cancellationToken, true);
    }, $"Requesting missing packet {sequenceNumber}", cancellationToken: cancellationToken);
}

async Task ReceivePacketsAsync(NetworkStream stream, CancellationToken cancellationToken, bool expectSinglePacket = false)
{
    var buffer = new byte[PACKET_SIZE];

    try
    {
        while (true)
        {
            int bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
                break;

            if (bytesRead == PACKET_SIZE)
            {
                ParsePackets(buffer);

                if (expectSinglePacket)
                    return; 
            }
            else
            {
                Console.WriteLine($"Received incomplete packet ({bytesRead} bytes): {BitConverter.ToString(buffer, 0, bytesRead)}");
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Timed out waiting for data from server.");
        throw;
    }
}

// Get all the missing sequence numbers assuming start as 1 and end as the sequence number of the last packet.
List<int> GetMissingPacketSequenceIds()
{
    var received = packets.Select(p => p.Sequence).ToHashSet();
    int max = received.Max();

    return [.. Enumerable.Range(1, max).Where(i => !received.Contains(i))];
}

// Parses the bytes to a packet object
void ParsePackets(byte[] buffer)
{
    string symbol = Encoding.ASCII.GetString(buffer, 0, 4);
    char side = (char)buffer[4];
    int quantity = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(5, 4));
    int price = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(9, 4));
    int sequence = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(13, 4));
    
    if (!packets.Any(p => p.Sequence == sequence))
    {
        Console.WriteLine($"Parsed packet: {symbol} {side} {quantity} {price} {sequence}");
        packets.Add(new Packet(symbol, side, quantity, price, sequence));
    }
}

async Task RetryAsync(Func<Task> operation, string description, int maxRetries = MAX_RETRYS, int baseDelayMs = BASE_RETRY_DELAY, CancellationToken cancellationToken = default)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            Console.WriteLine($"{description}, attempt {attempt}...");
            await operation();
            return; 
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            Console.WriteLine($"{description} failed on attempt {attempt}: {ex.Message}. Retrying...");
            await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1), cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{description} failed after {maxRetries} attempts: {ex.Message}");
            throw; 
        }
    }
}

class Packet(string symbol, char side, int quantity, int price, int sequence)
{
    public string Symbol { get; } = symbol;
    public char Side { get; } = side;
    public int Quantity { get; } = quantity;
    public int Price { get; } = price;
    public int Sequence { get; } = sequence;
}

public enum CallType
{
    StreamAllPackets = 1,
    ResendPackets = 2
}
