using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    // Move painted vertices without moving the control's raycast rectangle.
    internal sealed class UiControlInkOffset : BaseMeshEffect
    {
        private Vector2 _offset;
        private float _shear;

        internal void Set(Vector2 offset, float shear = 0f)
        {
            if (_offset == offset && Mathf.Approximately(_shear, shear))
                return;
            _offset = offset;
            _shear = shear;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || (_offset == Vector2.zero && _shear == 0f))
                return;
            UIVertex v = default(UIVertex);
            float middle = graphic.rectTransform.rect.center.y;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref v, i);
                v.position.x += _offset.x + (v.position.y - middle) * _shear;
                v.position.y += _offset.y;
                mesh.SetUIVertex(v, i);
            }
        }
    }
}
