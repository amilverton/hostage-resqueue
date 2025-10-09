using Microsoft.Extensions.DependencyInjection;
using Roads;
using Roads.Example;
using UnityEngine;

namespace Core
{
    public static class Di
    {
        private static ServiceProvider _serviceProvider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Init()
        {
            var serviceCollection = new ServiceCollection();

            // Register road generation services as Transient (new instance each time)
            serviceCollection.AddSingleton<IPathGenerator, BiasedRandomWalkGenerator>();
            serviceCollection.AddSingleton<IBranchGenerator, RandomBranchGenerator>();

            // Register road spawner as Transient (stateful, should not be shared)
            // Each ProceduralRoads instance needs its own spawner to avoid cross-contamination
            serviceCollection.AddSingleton<IRoadSpawner, UnityRoadSpawner>();
            
            
            // STORES WILL BE REGISTERED HERE
            RegisterStores(serviceCollection);
            
            // SERVICES WILL BE REGISTERED HERE
            RegisterServices(serviceCollection);

            // Build service provider
            _serviceProvider = serviceCollection.BuildServiceProvider();

            Debug.Log("DI Container initialized");
        }

        private static void RegisterStores(ServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IPointsStore, PointsStore>();
        }

        private static void RegisterServices(ServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IPointsService, PointsService>();
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
