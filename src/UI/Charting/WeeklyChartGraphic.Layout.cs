using System.Collections;
using UnityEngine;

namespace XingShangMod.UI.Charting
{
    public sealed partial class WeeklyChartGraphic
    {
        private static readonly WaitForEndOfFrame LayoutRefreshWait = new WaitForEndOfFrame();

        private Coroutine _layoutRefreshCoroutine;
        private int _layoutRefreshVersion;

        private void ScheduleLayoutRefresh()
        {
            if (_isDestroying || !isActiveAndEnabled || _layoutRefreshCoroutine != null)
                return;
            int version = ++_layoutRefreshVersion;
            _layoutRefreshCoroutine = StartCoroutine(RefreshAfterLayout(version));
        }

        private IEnumerator RefreshAfterLayout(int version)
        {
            yield return LayoutRefreshWait;
            if (version != _layoutRefreshVersion)
                yield break;
            _layoutRefreshCoroutine = null;
            if (_isDestroying || !isActiveAndEnabled)
                yield break;
            UpdateVisuals();
        }

        private void CancelLayoutRefresh()
        {
            _layoutRefreshVersion++;
            if (_layoutRefreshCoroutine == null)
                return;
            StopCoroutine(_layoutRefreshCoroutine);
            _layoutRefreshCoroutine = null;
        }
    }
}
