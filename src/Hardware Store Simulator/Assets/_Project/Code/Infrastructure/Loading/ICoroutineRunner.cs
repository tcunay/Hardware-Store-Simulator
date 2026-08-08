using System.Collections;
using UnityEngine;

namespace HardwareStore.Infrastructure.Loading
{
    public interface ICoroutineRunner
    {
        Coroutine StartCoroutine(IEnumerator routine);
    }
}
