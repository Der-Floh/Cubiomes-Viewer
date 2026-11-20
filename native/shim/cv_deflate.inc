/* Deflating the bands of a PNG's image data with libdeflate. libdeflate ends every stream with a final block, while the
   bands of an image follow each other in one zlib stream, so a band that more bands follow gets its final block turned
   into an ordinary one and ends with an empty stored block, as zlib's sync flush ends it. Finding the final block means
   walking the codes of every block of the band. */

#define CV_STORED_BLOCK 0
#define CV_FIXED_BLOCK 1
#define CV_DYNAMIC_BLOCK 2
#define CV_END_OF_BLOCK 256
#define CV_MAX_CODE_BITS 15
#define CV_LITERAL_CODES 288
#define CV_DISTANCE_CODES 30
#define CV_LENGTH_CODES 29
#define CV_CODE_LENGTH_CODES 19
#define CV_SYNC_FLUSH_BYTES 5

typedef struct cv_bit_reader
{
    const uint8_t *data;
    int64_t bits;
    int64_t position;
} cv_bit_reader;

typedef struct cv_huffman
{
    int16_t counts[CV_MAX_CODE_BITS + 1];
    int16_t symbols[CV_LITERAL_CODES];
} cv_huffman;

static const uint8_t cv_length_extra_bits[CV_LENGTH_CODES] = {
    0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };
static const uint8_t cv_distance_extra_bits[CV_DISTANCE_CODES] = {
    0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };
static const uint8_t cv_code_length_order[CV_CODE_LENGTH_CODES] = {
    16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

static int cv_read_bits(cv_bit_reader *reader, int count, uint32_t *value)
{
    if (reader->position + count > reader->bits)
        return 0;

    uint32_t result = 0;
    for (int bit = 0; bit < count; bit++, reader->position++)
        result |= (uint32_t)(reader->data[reader->position >> 3] >> (reader->position & 7) & 1) << bit;
    *value = result;
    return 1;
}

/* The canonical code of the code lengths; 0 when the lengths ask for more codes than there are. */
static int cv_build_huffman(cv_huffman *code, const uint8_t *lengths, int count)
{
    memset(code->counts, 0, sizeof(code->counts));
    for (int symbol = 0; symbol < count; symbol++)
        code->counts[lengths[symbol]]++;

    int left = 1;
    for (int length = 1; length <= CV_MAX_CODE_BITS; length++)
    {
        left = (left << 1) - code->counts[length];
        if (left < 0)
            return 0;
    }

    int16_t offsets[CV_MAX_CODE_BITS + 1];
    offsets[1] = 0;
    for (int length = 1; length < CV_MAX_CODE_BITS; length++)
        offsets[length + 1] = (int16_t)(offsets[length] + code->counts[length]);
    for (int symbol = 0; symbol < count; symbol++)
    {
        if (lengths[symbol] != 0)
            code->symbols[offsets[lengths[symbol]]++] = (int16_t)symbol;
    }
    return 1;
}

/* The next symbol, its code read one bit at a time from the most significant bit; -1 at the end of the data. */
static int cv_decode(cv_bit_reader *reader, const cv_huffman *code)
{
    int value = 0, first = 0, index = 0;
    for (int length = 1; length <= CV_MAX_CODE_BITS; length++)
    {
        uint32_t bit;
        if (!cv_read_bits(reader, 1, &bit))
            return -1;
        value |= (int)bit;
        int count = code->counts[length];
        if (value - first < count)
            return code->symbols[index + value - first];
        index += count;
        first = (first + count) << 1;
        value <<= 1;
    }
    return -1;
}

/* Reads the literals and matches of a block up to its end-of-block code. */
static int cv_skip_codes(cv_bit_reader *reader, const cv_huffman *literals, const cv_huffman *distances)
{
    for (;;)
    {
        int symbol = cv_decode(reader, literals);
        if (symbol < 0)
            return 0;
        if (symbol < CV_END_OF_BLOCK)
            continue;
        if (symbol == CV_END_OF_BLOCK)
            return 1;

        uint32_t extra;
        symbol -= CV_END_OF_BLOCK + 1;
        if (symbol >= CV_LENGTH_CODES || !cv_read_bits(reader, cv_length_extra_bits[symbol], &extra))
            return 0;
        int distance = cv_decode(reader, distances);
        if (distance < 0 || distance >= CV_DISTANCE_CODES || !cv_read_bits(reader, cv_distance_extra_bits[distance], &extra))
            return 0;
    }
}

static int cv_skip_stored_block(cv_bit_reader *reader)
{
    reader->position = (reader->position + 7) & ~(int64_t)7;
    uint32_t length, complement;
    if (!cv_read_bits(reader, 16, &length) || !cv_read_bits(reader, 16, &complement) || (length ^ 0xFFFFu) != complement)
        return 0;
    if (reader->position + (int64_t)length * 8 > reader->bits)
        return 0;

    reader->position += (int64_t)length * 8;
    return 1;
}

static int cv_skip_fixed_block(cv_bit_reader *reader)
{
    uint8_t lengths[CV_LITERAL_CODES];
    int symbol = 0;
    for (; symbol < 144; symbol++)
        lengths[symbol] = 8;
    for (; symbol < 256; symbol++)
        lengths[symbol] = 9;
    for (; symbol < 280; symbol++)
        lengths[symbol] = 7;
    for (; symbol < CV_LITERAL_CODES; symbol++)
        lengths[symbol] = 8;

    cv_huffman literals, distances;
    cv_build_huffman(&literals, lengths, CV_LITERAL_CODES);
    memset(lengths, 5, CV_DISTANCE_CODES);
    cv_build_huffman(&distances, lengths, CV_DISTANCE_CODES);
    return cv_skip_codes(reader, &literals, &distances);
}

static int cv_skip_dynamic_block(cv_bit_reader *reader)
{
    uint32_t literal_count, distance_count, code_length_count;
    if (!cv_read_bits(reader, 5, &literal_count) || !cv_read_bits(reader, 5, &distance_count) || !cv_read_bits(reader, 4, &code_length_count))
        return 0;
    literal_count += 257;
    distance_count += 1;
    code_length_count += 4;
    if (literal_count > CV_LITERAL_CODES || distance_count > CV_DISTANCE_CODES)
        return 0;

    uint8_t lengths[CV_LITERAL_CODES + CV_DISTANCE_CODES] = { 0 };
    for (uint32_t index = 0; index < code_length_count; index++)
    {
        uint32_t length;
        if (!cv_read_bits(reader, 3, &length))
            return 0;
        lengths[cv_code_length_order[index]] = (uint8_t)length;
    }

    cv_huffman code_lengths;
    if (!cv_build_huffman(&code_lengths, lengths, CV_CODE_LENGTH_CODES))
        return 0;

    uint32_t total = literal_count + distance_count;
    for (uint32_t index = 0; index < total;)
    {
        int symbol = cv_decode(reader, &code_lengths);
        if (symbol < 0)
            return 0;
        if (symbol < 16)
        {
            lengths[index++] = (uint8_t)symbol;
            continue;
        }

        uint32_t repeat;
        uint8_t value = 0;
        if (symbol == 16)
        {
            if (index == 0 || !cv_read_bits(reader, 2, &repeat))
                return 0;
            value = lengths[index - 1];
            repeat += 3;
        }
        else if (symbol == 17)
        {
            if (!cv_read_bits(reader, 3, &repeat))
                return 0;
            repeat += 3;
        }
        else
        {
            if (!cv_read_bits(reader, 7, &repeat))
                return 0;
            repeat += 11;
        }

        if (index + repeat > total)
            return 0;
        while (repeat-- > 0)
            lengths[index++] = value;
    }

    cv_huffman literals, distances;
    if (!cv_build_huffman(&literals, lengths, (int)literal_count) || !cv_build_huffman(&distances, lengths + literal_count, (int)distance_count))
        return 0;
    return cv_skip_codes(reader, &literals, &distances);
}

/* The bit positions of the final block's header and of the end of its codes in a raw deflate stream. */
static int cv_find_final_block(const uint8_t *stream, int64_t length, int64_t *header, int64_t *end)
{
    cv_bit_reader reader = { stream, length * 8, 0 };
    for (;;)
    {
        int64_t start = reader.position;
        uint32_t is_final, type;
        if (!cv_read_bits(&reader, 1, &is_final) || !cv_read_bits(&reader, 2, &type))
            return 0;

        int skipped = type == CV_STORED_BLOCK ? cv_skip_stored_block(&reader)
            : type == CV_FIXED_BLOCK ? cv_skip_fixed_block(&reader)
            : type == CV_DYNAMIC_BLOCK ? cv_skip_dynamic_block(&reader)
            : 0;
        if (!skipped)
            return 0;
        if (is_final)
        {
            *header = start;
            *end = reader.position;
            return 1;
        }
    }
}

/* Makes the final block an ordinary one and closes the stream with an empty stored block on a byte boundary. Everything
   after the final block's codes becomes zero: the stored block's header bits and its padding up to the boundary. */
static int64_t cv_end_with_sync_flush(uint8_t *stream, int64_t length, int64_t capacity)
{
    int64_t header, end;
    if (!cv_find_final_block(stream, length, &header, &end))
        return CV_ERR_STATE;

    int64_t aligned = (end + 3 + 7) >> 3;
    if (aligned + 4 > capacity)
        return CV_ERR_CAPACITY;

    stream[header >> 3] &= (uint8_t)~(1u << (header & 7));
    if ((end & 7) != 0)
        stream[end >> 3] &= (uint8_t)((1u << (end & 7)) - 1);
    for (int64_t index = (end + 7) >> 3; index < aligned; index++)
        stream[index] = 0;
    stream[aligned] = 0x00;
    stream[aligned + 1] = 0x00;
    stream[aligned + 2] = 0xFF;
    stream[aligned + 3] = 0xFF;
    return aligned + 4;
}

int64_t cv_deflate_bound(int64_t length)
{
    if (length < 0)
        return CV_ERR_ARGUMENT;
    return (int64_t)libdeflate_deflate_compress_bound(NULL, (size_t)length) + CV_SYNC_FLUSH_BYTES;
}

int64_t cv_deflate_band(const uint8_t *data, int64_t length, int32_t level, int32_t is_last, uint8_t *out, int64_t capacity)
{
    if (length < 0 || (!data && length > 0) || !out || capacity <= 0 || level < 0 || level > CV_DEFLATE_MAX_LEVEL)
        return CV_ERR_ARGUMENT;

    struct libdeflate_compressor *compressor = libdeflate_alloc_compressor(level);
    if (!compressor)
        return CV_ERR_MEMORY;
    size_t written = libdeflate_deflate_compress(compressor, data, (size_t)length, out, (size_t)capacity);
    libdeflate_free_compressor(compressor);
    if (written == 0)
        return CV_ERR_CAPACITY;

    return is_last ? (int64_t)written : cv_end_with_sync_flush(out, (int64_t)written, capacity);
}
