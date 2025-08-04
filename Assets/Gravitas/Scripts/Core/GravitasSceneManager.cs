using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gravitas
{
    /// <summary>
    /// Static class to handle all scene-related operations related to Gravitas simulation.
    /// </summary>
    public static class GravitasSceneManager
    {
        // Collection for associating physics scene objects with actual Unity scenes
        private static readonly Dictionary<GravitasPhysicsScene, Scene> sceneLookupTable =
            new Dictionary<GravitasPhysicsScene, Scene>();

        public static void ClearAllScenes()
        {
            sceneLookupTable?.Clear();
        }

        /// <summary>
        /// Moves the given GameObject to the scene associated with the given physics scene object.
        /// </summary>
        /// <param name="go">The GameObject to be moved</param>
        /// <param name="gravitasPhysicsScene">The physics scene to be moved to</param>
        public static void MoveGameObjectToPhysicsScene(GameObject go, GravitasPhysicsScene gravitasPhysicsScene)
        {
            if (sceneLookupTable.TryGetValue(gravitasPhysicsScene, out Scene scene))
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }
        }

        /// <summary>
        /// Convenience method for reloading the current active scene.
        /// </summary>
        public static void ReloadMainScene()
        {
            GravitasManager.UnloadAllFields();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Method to begin the unloading process of the given physics scene.
        /// </summary>
        /// <param name="gravitasPhysicsScene">The physics scene to unload</param>
        public static void UnloadGravitasPhysicsScene(GravitasPhysicsScene gravitasPhysicsScene)
        {
            if (sceneLookupTable.TryGetValue(gravitasPhysicsScene, out Scene scene))
            {
                sceneLookupTable.Remove(gravitasPhysicsScene);

                if (scene.IsValid())
                    SceneManager.UnloadSceneAsync(scene);
            }
        }

        /// <summary>
        /// Creates a Unity physics scene object for the GravitasPhysicsScene object, and adds it to the simulated scene collection.
        /// </summary>
        /// <param name="gravitasPhysicsScene">The physics scene object to associate with</param>
        /// <param name="originObject">The GameObject to be placed as the origin of the physics scene</param>
        /// <param name="sceneName">The name of the scene</param>
        /// <returns>PhysicsScene the Unity PhysicsScene object created</returns>
        public static PhysicsScene CreatePhysicsScene(GravitasPhysicsScene gravitasPhysicsScene, GameObject originObject, string sceneName)
        {
            const int ATTEMPTS = 100;

            CreateSceneParameters createSceneParameters = new CreateSceneParameters(LocalPhysicsMode.Physics3D);

            Scene scene;
            string physicsSceneName = $"{sceneName} Physics Scene";
            int attempt = 0;
            do
            {
                if (TryCreateScene(physicsSceneName, createSceneParameters, out scene))
                {
                    break;
                }

                attempt++;
                physicsSceneName = $"{sceneName} Physics Scene ({attempt})";
            } while (attempt < ATTEMPTS);

            if (attempt == ATTEMPTS)
            {
                Debug.LogError($"Gravitas: Unable to create scene \"{sceneName}\"");

                return default;
            }
            else
            {
                SceneManager.MoveGameObjectToScene(originObject, scene);

                PhysicsScene physicsScene = scene.GetPhysicsScene();
                sceneLookupTable.Add(gravitasPhysicsScene, scene);

                return physicsScene;
            }

            static bool TryCreateScene(string sceneName, CreateSceneParameters createSceneParameters, out Scene scene)
            {
                try
                {
                    scene = SceneManager.CreateScene(sceneName, createSceneParameters);

                    return true;
                }
                catch (System.ArgumentException)
                {
                    scene = default;

                    return false;
                }
            }
        }
    }
}
