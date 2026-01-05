// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Snap.Hutao.Core.Database;
using Snap.Hutao.Model.Entity.Database;

namespace Snap.Hutao.Service.Abstraction.Property;

internal sealed partial class ProtobufDbProperty<TMessage> : DbProperty<TMessage>
    where TMessage : class, IMessage<TMessage>, new()
{
    private readonly IServiceProvider serviceProvider;
    private readonly string key;
    private readonly Func<TMessage> defaultValueFactory;

    [field: MaybeNull]
    private TMessage? cached;

    public ProtobufDbProperty(IServiceProvider serviceProvider, string key, Func<TMessage> defaultValueFactory)
    {
        this.serviceProvider = serviceProvider;
        this.key = key;
        this.defaultValueFactory = defaultValueFactory;
    }

    public ProtobufDbProperty(IServiceProvider serviceProvider, string key, TMessage defaultValue)
        : this(serviceProvider, key, () => defaultValue)
    {
    }

    public override TMessage Value
    {
        get
        {
            if (cached is null)
            {
                using (IServiceScope scope = serviceProvider.CreateScope())
                {
                    AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    string? value = GetValue(appDbContext, key);
                    cached = string.IsNullOrEmpty(value)
                        ? defaultValueFactory()
                        : ParseFromBase64(value);
                }
            }

            return cached;
        }

        set
        {
            if (Volatile.Read(ref Deferring))
            {
                cached = value;
                SetValue(value);
            }
            else
            {
                if (SetProperty(ref cached, value))
                {
                    SetValue(value);
                }
            }
        }
    }

    protected override void SetValue(TMessage value)
    {
        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            appDbContext.Settings.Where(e => e.Key == key).ExecuteDelete();
            appDbContext.Settings.AddAndSave(new(key, ToBase64(value)));
        }
    }

    private static TMessage ParseFromBase64(string base64)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch
        {
            return new();
        }

        TMessage msg = new();
        msg.MergeFrom(bytes);
        return msg;
    }

    private static string ToBase64(TMessage message)
    {
        return Convert.ToBase64String(message.ToByteArray());
    }
}
