using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Presentation;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PlayAudioCuesSystem : IExecuteSystem
    {
        private readonly IAudioService _audio;
        private readonly IGroup<GameEntity> _audioEvents;
        private readonly List<GameEntity> _buffer = new(8);

        public PlayAudioCuesSystem(GameContext gameContext, IAudioService audio)
        {
            _audio = audio;
            _audioEvents = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.AudioCue));
        }

        public void Execute()
        {
            foreach (GameEntity audioEvent in _audioEvents.GetEntities(_buffer))
            {
                _audio.Play(audioEvent.AudioCue);
                audioEvent.Destroy();
            }
        }
    }
}
