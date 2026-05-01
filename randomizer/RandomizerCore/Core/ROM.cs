using System.Diagnostics;
using System.Text;
using RandomizerCore.Utilities.IO;
using RandomizerCore.Utilities.Logging;

namespace RandomizerCore.Core;

public class Rom
{
    public readonly string Path;
    public readonly Reader Reader;

    public readonly byte[] RomData;

    public bool IsDummy { get; private set; }

    /// Populated by the web server from the client's entityAddresses POST field
    /// when running in zero-ROM (dummy) mode.  Key = (area << 16)|(room << 8)|chest.
    public Dictionary<int, int>? EntityAddressMap { get; set; }

    public Rom(string filePath)
    {
        Path = filePath;
        var smallData = File.ReadAllBytes(filePath);
        if (smallData.Length >= 0x01000000)
        {
            RomData = smallData;
        }
        else
        {
            RomData = new byte[0x1000000];
            smallData.CopyTo(RomData, 0);
        }

        var stream = Stream.Synchronized(new MemoryStream(RomData));
        Reader = new Reader(stream);
        Debug.WriteLine("Read " + stream.Length + " bytes.");

        SetupRom();
    }

    /// Construct a Rom without reading the user's ROM file. Used by the
    /// zero-ROM web flow: a 16 MB zero buffer with the EU "BZMP" region marker
    /// at 0xAC so SetupRom() classifies the region correctly. RomData stays
    /// all-zero because no consumer should read vanilla bytes from it in this
    /// mode — the client owns the vanilla ROM and reconstructs the BPS itself.
    private Rom(bool _dummyCtor)
    {
        Path = string.Empty;
        RomData = new byte[0x1000000];
        Encoding.ASCII.GetBytes("BZMP", 0, 4, RomData, 0xAC);
        IsDummy = true;

        var stream = Stream.Synchronized(new MemoryStream(RomData));
        Reader = new Reader(stream);

        SetupRom();
    }

    public static Rom? Instance { get; private set; }

    public RegionVersion Version { get; private set; } = RegionVersion.None;
    public HeaderData Headers { get; private set; }

    public static void Initialize(string filePath)
    {
        Logger.Instance.LogInfo("Loading ROM");
        Instance = new Rom(filePath);
        Logger.Instance.LogInfo("ROM Loaded Successfully");
    }

    /// Initialize Rom.Instance without reading any ROM file. See the dummy ctor.
    public static void InitializeDummy()
    {
        Logger.Instance.LogInfo("Initializing dummy ROM (zero-ROM mode)");
        Instance = new Rom(true);
    }

    private void SetupRom()
    {
        // Determine game region and if valid ROM
        var regionBytes = Reader.ReadBytes(4, 0xAC);
        var region = Encoding.UTF8.GetString(regionBytes);
        Debug.WriteLine("Region detected: " + region);

        if (region == "BZMP") Version = RegionVersion.Eu;

        if (region == "BZMJ") Version = RegionVersion.Jp;

        if (region == "BZME") Version = RegionVersion.Us;

        if (Version != RegionVersion.None) Headers = new Header().GetHeaderAddresses(Version);
    }
}
