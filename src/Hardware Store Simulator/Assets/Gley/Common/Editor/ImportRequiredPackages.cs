using UnityEditor;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace Gley.Common.Editor
{
    public class ImportRequiredPackages
    {
        private static AddRequest _request;
        private static UnityAction<string> _updateMethod;
        private static Queue<string> _packageQueue = new Queue<string>();
        private static bool _waitingForUnlock = false;

        public static void ImportPackage(string packageToImport, UnityAction<string> updateMethod)
        {
            _updateMethod = updateMethod;
            _packageQueue.Enqueue(packageToImport);

            if (_request == null && !_waitingForUnlock)
            {
                InstallNext();
            }
        }

        private static void InstallNext()
        {
            if (_packageQueue.Count == 0)
            {
                _request = null;
                _waitingForUnlock = false;
                return;
            }

            _waitingForUnlock = true;
            // wait for next editor update cycle before starting next install
            EditorApplication.delayCall += StartNextInstall;
        }

        private static void StartNextInstall()
        {
            EditorApplication.delayCall -= StartNextInstall;
            _waitingForUnlock = false;

            if (_packageQueue.Count == 0)
            {
                _request = null;
                return;
            }

            string package = _packageQueue.Dequeue();
            Debug.Log("Installing: " + package + ". Please wait...");
            _request = UnityEditor.PackageManager.Client.Add(package);
            EditorApplication.update += Progress;
        }

        private static void Progress()
        {
            if (_request == null)
                return;

            _updateMethod(_request.Status.ToString());

            if (_request.IsCompleted)
            {
                EditorApplication.update -= Progress;

                if (_request.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    _updateMethod("Installed: " + _request.Result.packageId);
                    Debug.Log("Installed: " + _request.Result.packageId);
                }
                else if (_request.Status >= UnityEditor.PackageManager.StatusCode.Failure)
                {
                    Debug.LogError(_request.Error.message);
                    _updateMethod(_request.Error.message);
                }

                _request = null;
                // delay before starting next to let unity release the package manager lock
                EditorApplication.delayCall += StartNextInstall;
            }
        }
    }
}