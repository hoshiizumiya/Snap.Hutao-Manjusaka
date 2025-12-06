// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.Abstraction;
using Snap.Hutao.Model.Metadata.Avatar;
using Snap.Hutao.Model.Metadata.Converter;
using Snap.Hutao.Model.Primitive;
using Snap.Hutao.ViewModel.AvatarProperty;
using Snap.Hutao.Service.Notification;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Snap.Hutao.Service.AvatarInfo.Factory.Builder;

internal static class AvatarViewBuilderSkillExtension
{
    public static TBuilder SetSkills<TBuilder>(this TBuilder builder, ImmutableArray<ProudSkill> proudSkills, FrozenDictionary<SkillId, SkillLevel> skillLevels, FrozenDictionary<SkillId, SkillLevel> extraLevels)
        where TBuilder : class, IAvatarViewBuilder
    {
        return builder.SetSkills(CreateSkills(proudSkills, skillLevels, extraLevels));
    }

    public static TBuilder SetSkills<TBuilder>(this TBuilder builder, ImmutableArray<SkillView> skills)
        where TBuilder : class, IAvatarViewBuilder
    {
        return builder.Configure(b => b.View.Skills = skills);
    }

    private static ImmutableArray<SkillView> CreateSkills(ImmutableArray<ProudSkill> proudSkills, FrozenDictionary<SkillId, SkillLevel> skillLevels, FrozenDictionary<SkillId, SkillLevel> extraLevels)
    {
        if (skillLevels is { Count: 0 })
        {
            return [];
        }

        SkillState state = new(skillLevels, extraLevels);

        // 探测缺失的映射并通知用户（避免索引异常导致闪退）
        try
        {
            // 延迟解析 messenger，减小对调用方的影响
            IMessenger? messenger = null;

            // 收集缺失的 skill id（只收集显式缺失的项，合并后一次通知）
            List<SkillId> missing = [];

            foreach (ProudSkill proudSkill in proudSkills)
            {
                if (!state.NonExtraLeveledSkills.ContainsKey(proudSkill.Id) && !state.SkillLevels.ContainsKey(proudSkill.Id))
                {
                    missing.Add(proudSkill.Id);
                }
            }

            if (missing.Count > 0)
            {
                try
                {
                    messenger ??= Ioc.Default.GetRequiredService<IMessenger>();
                    messenger.Send(InfoBarMessage.Warning("技能数据缺失", $"缺失技能等级映射: {string.Join(", ", missing)}"));
                }
                catch
                {
                    // 通知不可用时忽略，不影响后续构建
                }
            }
        }
        catch
        {
            // 保护性吞掉任何探测过程中的异常，保证不抛出
        }

        return proudSkills.SelectAsArray(
            static (proudSkill, state) =>
            {
                // 安全读取，避免 KeyNotFoundException
                state.NonExtraLeveledSkills.TryGetValue(proudSkill.Id, out SkillLevel baseLevel);
                state.ExtraLevels.TryGetValue(proudSkill.Id, out SkillLevel extraLevel);
                state.SkillLevels.TryGetValue(proudSkill.Id, out SkillLevel skillLevelForInfo);

                // 为 DescriptionsParametersDescriptor.Convert 准备合法的等级值：
                // 优先使用完整的 skillLevels（skillLevelForInfo），
                // 否则使用 base + extra（如果存在），
                // 否则退回到 1，避免 0 导致索引异常。
                uint infoLevel = skillLevelForInfo;
                uint combined = baseLevel + extraLevel;
                if (infoLevel == 0 && combined > 0)
                {
                    infoLevel = combined;
                }

                if (infoLevel == 0)
                {
                    infoLevel = 1;
                }

                return new SkillViewBuilder()
                    .SetName(proudSkill.Name)
                    .SetIcon(SkillIconConverter.IconNameToUri(proudSkill.Icon))
                    .SetDescription(proudSkill.Description)
                    .SetGroupId(proudSkill.GroupId)
                    .SetLevel(LevelFormat.Format(baseLevel, extraLevel))
                    .SetLevelNumber(baseLevel)
                    .SetInfo(DescriptionsParametersDescriptor.Convert(proudSkill.Proud, infoLevel))
                    .View;
            },
            state);
    }

    private sealed class SkillState
    {
        public SkillState(FrozenDictionary<SkillId, SkillLevel> skillLevels, FrozenDictionary<SkillId, SkillLevel> extraLevels)
        {
            SkillLevels = skillLevels;
            ExtraLevels = extraLevels;

            // Parameters has to be IDictionary<,> to avoid using IEnumerable<,> in the constructor
            Dictionary<SkillId, SkillLevel> nonExtraLeveledSkills = new(skillLevels);
            foreach ((SkillId skillId, SkillLevel extraLevel) in extraLevels)
            {
                nonExtraLeveledSkills.DecreaseByValue(skillId, extraLevel);
            }

            NonExtraLeveledSkills = nonExtraLeveledSkills;
        }

        public IReadOnlyDictionary<SkillId, SkillLevel> SkillLevels { get; }

        public IReadOnlyDictionary<SkillId, SkillLevel> ExtraLevels { get; }

        public IReadOnlyDictionary<SkillId, SkillLevel> NonExtraLeveledSkills { get; }
    }
}