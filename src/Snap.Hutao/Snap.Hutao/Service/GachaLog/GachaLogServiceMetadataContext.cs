// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Model;
using Snap.Hutao.Model.Metadata;
using Snap.Hutao.Model.Metadata.Abstraction;
using Snap.Hutao.Model.Metadata.Avatar;
using Snap.Hutao.Model.Metadata.Weapon;
using Snap.Hutao.Model.Primitive;
using Snap.Hutao.Service.Metadata.ContextAbstraction;
using Snap.Hutao.Service.Metadata.ContextAbstraction.ImmutableArray;
using Snap.Hutao.Service.Metadata.ContextAbstraction.ImmutableDictionary;
using Snap.Hutao.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;
using Snap.Hutao.Model.Intrinsic;
using Snap.Hutao.Service.Notification;

namespace Snap.Hutao.Service.GachaLog;

internal sealed class GachaLogServiceMetadataContext : IMetadataContext,
    IMetadataArrayGachaEventSource,
    IMetadataDictionaryIdAvatarSource,
    IMetadataDictionaryIdWeaponSource,
    IMetadataDictionaryNameAvatarSource,
    IMetadataDictionaryNameWeaponSource
{
    public ImmutableArray<GachaEvent> GachaEvents { get; set; } = default!;

    public ImmutableDictionary<AvatarId, Avatar> IdAvatarMap { get; set; } = default!;

    public ImmutableDictionary<WeaponId, Weapon> IdWeaponMap { get; set; } = default!;

    public ImmutableDictionary<string, Avatar> NameAvatarMap { get; set; } = default!;

    public ImmutableDictionary<string, Weapon> NameWeaponMap { get; set; } = default!;

    public Item GetItemByNameAndType(string name, string type)
    {
        if (string.Equals(type, SH.ModelInterchangeUIGFItemTypeAvatar, StringComparison.Ordinal))
        {
            return this.GetAvatar(name).GetOrCreateItem();
        }

        if (string.Equals(type, SH.ModelInterchangeUIGFItemTypeWeapon, StringComparison.Ordinal))
        {
            return this.GetWeapon(name).GetOrCreateItem();
        }

        throw HutaoException.NotSupported($"Unsupported item type and name: '{type},{name}'");
    }

    public uint GetItemId(GachaLogItem item)
    {
        if (string.Equals(item.ItemType, SH.ModelInterchangeUIGFItemTypeAvatar, StringComparison.Ordinal))
        {
            return this.GetAvatar(item.Name).Id;
        }

        if (string.Equals(item.ItemType, SH.ModelInterchangeUIGFItemTypeWeapon, StringComparison.Ordinal))
        {
            return this.GetWeapon(item.Name).Id;
        }

        throw HutaoException.NotSupported($"Unsupported item type and name: '{item.ItemType},{item.Name}'");
    }

    public INameQualityAccess GetNameQualityByItemId(uint id)
    {
        uint place = id.StringLength;
        switch (place)
        {
            case 8U:
            {
                if (IdAvatarMap.TryGetValue(id, out Avatar? avatar))
                {
                    return avatar;
                }

                // notify user and return placeholder
                try
                {
                    Ioc.Default.GetRequiredService<IMessenger>().Send(InfoBarMessage.Warning(SH.ServiceGachaLogAvatarIdNotFound ?? "Avatar id not found", $"{id}"));
                }
                catch
                {
                    // ignore notification failures
                }

                return new UnknownNameQuality($"Unknown Avatar ({id})", QualityType.QUALITY_NONE);
            }

            case 5U:
            {
                if (IdWeaponMap.TryGetValue(id, out Weapon? weapon))
                {
                    return weapon;
                }

                try
                {
                    Ioc.Default.GetRequiredService<IMessenger>().Send(InfoBarMessage.Warning(SH.ServiceGachaLogWeaponIdNotFound ?? "Weapon id not found", $"{id}"));
                }
                catch
                {
                }

                return new UnknownNameQuality($"Unknown Weapon ({id})", QualityType.QUALITY_NONE);
            }

            default:
            {
                try
                {
                    Ioc.Default.GetRequiredService<IMessenger>().Send(InfoBarMessage.Warning(SH.ServiceGachaLogIdPlacesUnsupported ?? "Unsupported id places", $"{id} has places {place}"));
                }
                catch
                {
                }

                return new UnknownNameQuality($"Unknown ({id})", QualityType.QUALITY_NONE);
            }
        }
    }

    private sealed class UnknownNameQuality : INameQualityAccess
    {
        public UnknownNameQuality(string name, QualityType quality)
        {
            Name = name;
            Quality = quality;
        }

        public string Name { get; }

        public QualityType Quality { get; }
    }
}