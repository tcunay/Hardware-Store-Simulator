using UnityEngine;

namespace HardwareStore.Gameplay.Views
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class InteractionHighlight : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer _renderer;
        private MaterialPropertyBlock[] _propertyBlocks;
        private Color[] _baseColors;
        private bool[] _supportsBaseColor;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            Material[] materials = _renderer.sharedMaterials;
            _propertyBlocks = new MaterialPropertyBlock[materials.Length];
            _baseColors = new Color[materials.Length];
            _supportsBaseColor = new bool[materials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                _propertyBlocks[i] = new MaterialPropertyBlock();
                Material material = materials[i];
                if (material == null || !material.HasProperty(BaseColorId))
                    continue;

                _supportsBaseColor[i] = true;
                _baseColors[i] = material.GetColor(BaseColorId);
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            for (int i = 0; i < _propertyBlocks.Length; i++)
            {
                if (!_supportsBaseColor[i])
                    continue;

                MaterialPropertyBlock propertyBlock = _propertyBlocks[i];
                _renderer.GetPropertyBlock(propertyBlock, i);
                Color color = highlighted
                    ? Color.Lerp(_baseColors[i], Color.white, 0.38f)
                    : _baseColors[i];
                propertyBlock.SetColor(BaseColorId, color);
                _renderer.SetPropertyBlock(propertyBlock, i);
            }
        }

        private void OnDisable()
        {
            if (_propertyBlocks != null)
                SetHighlighted(false);
        }
    }
}
