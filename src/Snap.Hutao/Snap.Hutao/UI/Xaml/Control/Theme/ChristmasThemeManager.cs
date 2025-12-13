// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;

namespace Snap.Hutao.UI.Xaml.Control.Theme;

/// <summary>
/// Manager for applying and removing the Christmas theme. Need to clean.
/// </summary>
public static class ChristmasThemeManager
{
    /// <summary>
    /// Apply the Christmas theme to the application.
    /// </summary>
    /// <param name="app">The application instance.</param>
    public static void Apply(Application app)
    {
        if (app == null)
        {
            return;
        }

        try
        {
            string christmasUri = "ms-appx:///UI/Xaml/Control/Theme/Christmas.xaml";

            // Check if already applied
            foreach (ResourceDictionary? md in app.Resources.MergedDictionaries)
            {
                if (md.Source != null && md.Source.OriginalString.Equals(christmasUri, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Already applied
                }
            }

            ResourceDictionary rd = new ResourceDictionary
            {
                Source = new Uri(christmasUri)
            };

            app.Resources.MergedDictionaries.Add(rd);
        }
        catch
        {
            // Swallow errors to avoid breaking startup
        }
    }

    /// <summary>
    /// Remove the Christmas theme from the application.
    /// </summary>
    /// <param name="app">The application instance.</param>
    public static void Remove(Application app)
    {
        if (app == null)
        {
            return;
        }

        try
        {
            string christmasUri = "ms-appx:///UI/Xaml/Control/Theme/Christmas.xaml";

            for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                ResourceDictionary md = app.Resources.MergedDictionaries[i];
                if (md.Source != null && md.Source.OriginalString.Equals(christmasUri, StringComparison.OrdinalIgnoreCase))
                {
                    app.Resources.MergedDictionaries.RemoveAt(i);
                    return; // Only one instance should exist
                }
            }
        }
        catch
        {
            // Swallow errors
        }
    }
}
