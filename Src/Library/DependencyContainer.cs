using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;

namespace Hallupa.Library
{
    public static class DependencyContainer
    {
        private static AggregateCatalog _catalog;

        private static CompositionContainer _container;

        static DependencyContainer()
        {
            _catalog = new AggregateCatalog();
            _container = new CompositionContainer(_catalog);
        }

        public static void AddAssembly(string path)
        {
            _catalog.Catalogs.Add(new AssemblyCatalog(Assembly.LoadFrom(path)));
        }

        public static void AddAssembly(Assembly assembly)
        {
            _catalog.Catalogs.Add(new AssemblyCatalog(assembly));
        }

        public static CompositionContainer Container => _container;

        public static void ComposeParts(object obj)
        {
            Container.ComposeParts(obj);
        }
    }
}