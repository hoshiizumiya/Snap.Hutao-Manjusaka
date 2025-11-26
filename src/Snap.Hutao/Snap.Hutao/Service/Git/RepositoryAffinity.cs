// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.IO.Hashing;
using Snap.Hutao.Core.Setting;
using Snap.Hutao.Web.Hutao;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Snap.Hutao.Service.Git;

internal static class RepositoryAffinity
{
    private const string RepositoryAffinityPrefix = "RepositoryAffinity::";
    private static readonly Lock SyncRoot = new();

    public static ImmutableArray<GitRepository> Sort(ImmutableArray<GitRepository> repositories)
    {
        lock (SyncRoot)
        {
            int[] counts = new int[repositories.Length];
            for (int i = 0; i < repositories.Length; i++)
            {
                GitRepository repository = repositories[i];
                string key = GetSettingKey(repository.Name, repository.HttpsUrl.OriginalString);

                // 对读取值做下限保护，确保排序使用的是非负失败计数
                int raw = LocalSetting.Get(key, 0);
                counts[i] = Math.Max(0, raw);
            }

            Array.Sort(counts, ImmutableCollectionsMarshal.AsArray(repositories));
            return repositories;
        }
    }

    public static void IncreaseFailure(GitRepository repository)
    {
        IncreaseFailure(repository.Name, repository.HttpsUrl.OriginalString);
    }

    public static void IncreaseFailure(string name, string url)
    {
        lock (SyncRoot)
        {
            string key = GetSettingKey(name, url);
            int currentCount = LocalSetting.Get(key, 0);

            // 防止整数上溢：当已到达 int.MaxValue 时不再自增
            if (currentCount < int.MaxValue)
            {
                LocalSetting.Set(key, currentCount + 1);
            }
        }
    }

    public static void DecreaseFailure(GitRepository repository)
    {
        DecreaseFailure(repository.Name, repository.HttpsUrl.OriginalString);
    }

    public static void DecreaseFailure(string name, string url)
    {
        lock (SyncRoot)
        {
            string key = GetSettingKey(name, url);
            int currentCount = LocalSetting.Get(key, 0);

            // 失败次数不允许小于 0，避免出现负数或整型下溢
            if (currentCount > 0)
            {
                LocalSetting.Set(key, currentCount - 1);
            }
        }
    }

    private static string GetSettingKey(string name, string url)
    {
        string urlHash = Hash.ToHexString(HashAlgorithmName.SHA256, url.ToUpperInvariant());
        return $"{RepositoryAffinityPrefix}{name}::{urlHash}";
    }
}
