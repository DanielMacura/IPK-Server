using System.Collections.Concurrent;

namespace ipkcpd;

///
/// Author: Jonathan Dickinson
/// https://web.archive.org/web/20160728022524/http://blog.dickinsons.co.za/tips/2015/06/01/Net-Sockets-and-You/
/// 

/// <summary>
/// Represents a buffer pool.
/// </summary>
public class BufferPool
{
    private readonly int _segmentsPerChunk;
    private readonly int _segmentSize;
    private readonly ConcurrentStack<ArraySegment<byte>> _buffers;

    /// <summary>
    /// Gets the default instance of the buffer pool.
    /// </summary>
    public static readonly BufferPool Instance = new BufferPool(
        64,
        4096, /* Page size on Windows NT */
        64
        );

    /// <summary>
    /// Gets the segment size of the buffer pool.
    /// </summary>
    public int SegmentSize
    {
        get
        {
            return _segmentSize;
        }
    }

    /// <summary>
    /// Gets the amount of segments per chunk.
    /// </summary>
    public int SegmentsPerChunk
    {
        get
        {
            return _segmentsPerChunk;
        }
    }

    /// <summary>
    /// Creates a new chunk, makes buffers available.
    /// </summary>
    private void CreateNewChunk()
    {
        var val = _segmentsPerChunk * _segmentSize;

        byte[] bytes = new byte[val];
        for (int i = 0; i < _segmentsPerChunk; i++)
        {
            ArraySegment<byte> chunk = new
                ArraySegment<byte>(bytes, i * _segmentSize, _segmentSize);
            _buffers.Push(chunk);
        }
    }

    /// <summary>
    /// Creates a new chunk, makes buffers available.
    /// </summary>
    private void CompleteChunk(byte[] bytes)
    {
        for (int i = 1; i < _segmentsPerChunk; i++)
        {
            ArraySegment<byte> chunk = new
                ArraySegment<byte>(bytes, i * _segmentSize, _segmentSize);
            _buffers.Push(chunk);
        }
    }

    /// <summary>
    /// Checks out a buffer from the manager.
    /// </summary>
    /// <remarks>
    /// It is the client's responsibility to return the buffer to the manger by
    /// calling <see cref="CheckIn" /> on the buffer.
    /// </remarks>
    /// <returns>A <see cref="ArraySegment{Byte}" /> that can be used as a buffer.</returns>
    public ArraySegment<byte> Checkout()
    {
        ArraySegment<byte> seg = default(ArraySegment<byte>);
        if (!_buffers.TryPop(out seg))
        {
            // Allow the caller to continue as soon as possible.
            var chunk = new byte[_segmentsPerChunk * _segmentSize];
            var action = new Action<byte[]>(CompleteChunk);
            action.BeginInvoke(chunk, x => action.EndInvoke(x), null);
            // We have the buffer at the start of the chunk.
            seg = new ArraySegment<byte>(chunk, 0, _segmentsPerChunk);
        }

        return seg;
    }

    /// <summary>
    /// Returns a buffer to the control of the manager.
    /// </summary>
    /// <param name="buffer">The <see cref="ArraySegment{Byte}"></see> to return to the cache.</param>
    public void CheckIn(ArraySegment<byte> buffer)
    {
        if (buffer.Array == null)
            throw new ArgumentNullException("buffer.Array");
        _buffers.Push(buffer);
    }

    /// <summary>
    /// Constructs a new <see cref="BufferPool" /> object
    /// </summary>
    /// <param name="segmentChunks">The number of chunks to create per segment</param>
    /// <param name="chunkSize">The size of a chunk in bytes</param>
    public BufferPool(int segmentChunks, int chunkSize) :
        this(segmentChunks, chunkSize, 1)
    {

    }

    /// <summary>
    /// Constructs a new <see cref="BufferPool"></see> object
    /// </summary>
    /// <param name="segmentsPerChunk">The number of segments per chunk.</param>
    /// <param name="segmentSize">The size of each segment.</param>
    /// <param name="initialChunks">The initial number of chunks to create.</param>
    public BufferPool(int segmentsPerChunk, int segmentSize, int initialChunks)
    {
        _segmentsPerChunk = segmentsPerChunk;
        _segmentSize = segmentSize;
        _buffers = new ConcurrentStack<ArraySegment<byte>>();
        for (int i = 0; i < initialChunks; i++)
        {
            CreateNewChunk();
        }
    }
}