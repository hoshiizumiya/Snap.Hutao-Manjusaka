namespace Snap.Hutao.Service.Yae.Metadata;

internal static class Crc32
{
    private const uint Polynomial = 0xEDB88320;
    private static readonly uint[] Crc32Table = new uint[256];

    static Crc32()
    {
        for (uint i = 0; i < Crc32Table.Length; i++)
        {
            uint value = i;
            for (int j = 0; j < 8; j++)
            {
                value = (value >> 1) ^ ((value & 1) * Polynomial);
            }

            Crc32Table[i] = value;
        }
    }

    public static uint Compute(Span<byte> buffer)
    {
        uint checksum = 0xFFFFFFFF;
        foreach (byte b in buffer)
        {
            checksum = (checksum >> 8) ^ Crc32Table[(b ^ checksum) & 0xFF];
        }

        return ~checksum;
    }
}
