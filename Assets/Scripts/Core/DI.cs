using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Roads
{
    public static class DI
    {
        private static ServiceProvider _serviceProvider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Init()
        {
            var serviceCollection = new ServiceCollection();

            // Register road generation services as Transient (new instance each time)
            serviceCollection.AddTransient<IPathGenerator, BiasedRandomWalkGenerator>();
            serviceCollection.AddTransient<IBranchGenerator, RandomBranchGenerator>();

            // Register road spawner as Transient (stateful, should not be shared)
            // Each ProceduralRoads instance needs its own spawner to avoid cross-contamination
            serviceCollection.AddTransient<IRoadSpawner, UnityRoadSpawner>();

            // Build service provider
            _serviceProvider = serviceCollection.BuildServiceProvider();

            Debug.Log("DI Container initialized");
        }

        public static T GetService<T>()
        {
            // Lazy initialization guard for edit-mode and unit tests
            if (_serviceProvider == null)
            {
                Init();
            }

            return _serviceProvider.GetRequiredService<T>();
        }
    }
}
