using System;

namespace XingShangMod.App
{
    public interface IModUserInterface
    {
        bool TryOpenMarket(Action onClosed);
        bool IsMarketOpen { get; }
        void OpenReadonlyInventory();
        void CloseAll();
        void Refresh();
    }
}
