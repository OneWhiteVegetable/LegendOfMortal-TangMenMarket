using System;
using XingShangMod.App;

namespace XingShangMod.Patches
{
    internal static class PatchGuard
    {
        internal static void Run(string source, Action<ModApplication> action)
        {
            ModApplication app = ModApplication.Instance;
            if (app == null || !app.IsEnabled)
                return;
            try
            {
                action(app);
            }
            catch (Exception exception)
            {
                app.HandleIntegrationError(source, exception);
            }
        }
    }
}
