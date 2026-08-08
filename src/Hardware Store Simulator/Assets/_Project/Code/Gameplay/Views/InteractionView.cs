using System;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Views
{
    [DisallowMultipleComponent]
    public class InteractionView : EntityBehaviour
    {
        [SerializeField] private InteractionHighlight _highlight;

        protected virtual void Awake()
        {
            if (_highlight == null)
                throw new MissingReferenceException($"{name} requires an interaction highlight.");
        }

        public void Configure(InteractionHighlight highlight)
        {
            _highlight = highlight != null
                ? highlight
                : throw new ArgumentNullException(nameof(highlight));
        }

        public void SetHighlighted(bool highlighted)
        {
            _highlight.SetHighlighted(highlighted);
        }
    }
}
