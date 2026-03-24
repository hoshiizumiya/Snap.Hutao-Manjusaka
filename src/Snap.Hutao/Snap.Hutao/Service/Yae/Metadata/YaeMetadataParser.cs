using Google.Protobuf;
using Snap.Hutao.Core.Protobuf;
using Snap.Hutao.Service.Yae.Achievement;

namespace Snap.Hutao.Service.Yae.Metadata;

internal static class YaeMetadataParser
{
    private const uint AchievementInfoNativeConfigTag = 42; // field 5, wire type length-delimited
    private const uint NativeConfigStoreCmdIdTag = 8; // field 1, varint
    private const uint NativeConfigAchievementCmdIdTag = 16; // field 2, varint
    private const uint NativeConfigMethodRvaTag = 82; // field 10, length-delimited

    private const uint MapEntryKeyTag = 8; // field 1, varint
    private const uint MapEntryValueTag = 18; // field 2, length-delimited

    private const uint MethodRvaDoCmdTag = 8;
    private const uint MethodRvaUpdateNormalPropTag = 24;
    private const uint MethodRvaNewStringTag = 32;
    private const uint MethodRvaFindGameObjectTag = 40;
    private const uint MethodRvaEventSystemUpdateTag = 48;
    private const uint MethodRvaSimulatePointerClickTag = 56;
    private const uint MethodRvaToInt32Tag = 64;
    private const uint MethodRvaTcpStatePtrTag = 72;
    private const uint MethodRvaSharedInfoPtrTag = 80;
    private const uint MethodRvaDecompressTag = 88;

    public static YaeNativeLibConfig? ParseNativeLibConfig(byte[] data)
    {
        uint storeCmdId = 0;
        uint achievementCmdId = 0;
        Dictionary<uint, MethodRva> methodRva = [];
        bool hasNativeConfig = false;

        CodedInputStream input = new(data);
        while (input.TryReadTag(out uint tag))
        {
            switch (tag)
            {
                case AchievementInfoNativeConfigTag:
                    hasNativeConfig = true;
                    using (CodedInputStream nativeConfigStream = input.UnsafeReadLengthDelimitedStream())
                    {
                        ParseNativeConfig(nativeConfigStream, ref storeCmdId, ref achievementCmdId, methodRva);
                    }
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        if (!hasNativeConfig || methodRva.Count == 0)
        {
            return null;
        }

        return new YaeNativeLibConfig
        {
            StoreCmdId = storeCmdId,
            AchievementCmdId = achievementCmdId,
            MethodRva = methodRva,
        };
    }

    private static void ParseNativeConfig(CodedInputStream input, ref uint storeCmdId, ref uint achievementCmdId, Dictionary<uint, MethodRva> methodRva)
    {
        while (input.TryReadTag(out uint tag))
        {
            switch (tag)
            {
                case NativeConfigStoreCmdIdTag:
                    storeCmdId = input.ReadUInt32();
                    break;
                case NativeConfigAchievementCmdIdTag:
                    achievementCmdId = input.ReadUInt32();
                    break;
                case NativeConfigMethodRvaTag:
                    using (CodedInputStream entryStream = input.UnsafeReadLengthDelimitedStream())
                    {
                        ParseMethodRvaEntry(entryStream, methodRva);
                    }
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
    }

    private static void ParseMethodRvaEntry(CodedInputStream input, Dictionary<uint, MethodRva> methodRva)
    {
        uint key = 0;
        MethodRva? value = null;

        while (input.TryReadTag(out uint tag))
        {
            switch (tag)
            {
                case MapEntryKeyTag:
                    key = input.ReadUInt32();
                    break;
                case MapEntryValueTag:
                    using (CodedInputStream valueStream = input.UnsafeReadLengthDelimitedStream())
                    {
                        value = ParseMethodRvaConfig(valueStream);
                    }
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        if (value is not null)
        {
            methodRva[key] = value;
        }
    }

    private static MethodRva ParseMethodRvaConfig(CodedInputStream input)
    {
        uint doCmd = 0;
        uint updateNormalProp = 0;
        uint newString = 0;
        uint findGameObject = 0;
        uint eventSystemUpdate = 0;
        uint simulatePointerClick = 0;
        uint toInt32 = 0;
        uint tcpStatePtr = 0;
        uint sharedInfoPtr = 0;
        uint decompress = 0;

        while (input.TryReadTag(out uint tag))
        {
            switch (tag)
            {
                case MethodRvaDoCmdTag:
                    doCmd = input.ReadUInt32();
                    break;
                case MethodRvaUpdateNormalPropTag:
                    updateNormalProp = input.ReadUInt32();
                    break;
                case MethodRvaNewStringTag:
                    newString = input.ReadUInt32();
                    break;
                case MethodRvaFindGameObjectTag:
                    findGameObject = input.ReadUInt32();
                    break;
                case MethodRvaEventSystemUpdateTag:
                    eventSystemUpdate = input.ReadUInt32();
                    break;
                case MethodRvaSimulatePointerClickTag:
                    simulatePointerClick = input.ReadUInt32();
                    break;
                case MethodRvaToInt32Tag:
                    toInt32 = input.ReadUInt32();
                    break;
                case MethodRvaTcpStatePtrTag:
                    tcpStatePtr = input.ReadUInt32();
                    break;
                case MethodRvaSharedInfoPtrTag:
                    sharedInfoPtr = input.ReadUInt32();
                    break;
                case MethodRvaDecompressTag:
                    decompress = input.ReadUInt32();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return new MethodRva
        {
            DoCmd = doCmd,
            UpdateNormalProperty = updateNormalProp,
            NewString = newString,
            FindGameObject = findGameObject,
            EventSystemUpdate = eventSystemUpdate,
            SimulatePointerClick = simulatePointerClick,
            ToInt32 = toInt32,
            TcpStatePtr = tcpStatePtr,
            SharedInfoPtr = sharedInfoPtr,
            Decompress = decompress,
        };
    }
}
