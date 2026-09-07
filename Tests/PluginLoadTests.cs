using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Linq;
using System.Runtime.Loader;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class PluginLoadTests
{
    [Fact]
    public void JellyfinAssemblyRegistersAnInstantiableSubtitleProvider()
    {
        // Exercise the same LoadFromAssemblyPath operation used by Jellyfin.
        // The test host runs on .NET 9 with the Jellyfin 10.11 runtime contracts.
        var context = new AssemblyLoadContext("Jimakufin load check", isCollectible: true);
        context.Resolving += (_, name) => name.Name == "Jimaku.Shared"
            ? throw new InvalidOperationException("Plugin must not load a shared DLL.")
            : Assembly.Load(name);
        var package = Environment.GetEnvironmentVariable("JIMAKU_TEST_PACKAGE");
        var directory = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(package))
        {
            directory = Path.Combine(Path.GetTempPath(), "jimakufin-load-" + Guid.NewGuid().ToString("N"));
            ZipFile.ExtractToDirectory(package, directory);
            Assert.Equal(new[] { "Jellyfin.Jimakufin.dll" }, Directory.GetFiles(directory).Select(Path.GetFileName));
        }
        try
        {
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "Jellyfin.Jimakufin.dll"));
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name == "Jimaku.Shared");
            Assert.Same(assembly, assembly.GetType("Jimaku.Shared.JimakuClient", throwOnError: true).Assembly);
            var provider = Assert.Single(assembly.GetTypes(), t => typeof(ISubtitleProvider).IsAssignableFrom(t));
            Assert.True(provider.IsPublic);
            Assert.Single(provider.GetConstructors());
            Assert.Contains("Jellyfin.Jimakufin.Configuration.config.html", assembly.GetManifestResourceNames());

            // Jellyfin 10.11 obtains subtitle providers from DI, not GetExports<ISubtitleProvider>().
            // Loading the DLL and finding the type is not sufficient to appear in the downloader list.
            var registratorType = Assert.Single(assembly.GetTypes(), t => typeof(IPluginServiceRegistrator).IsAssignableFrom(t));
            var registrator = (IPluginServiceRegistrator)Activator.CreateInstance(registratorType);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(DispatchProxy.Create<ILibraryManager, UnusedLibraryProxy>());
            registrator.RegisterServices(services, null);
            using var serviceProvider = services.BuildServiceProvider();
            var registered = Assert.Single(serviceProvider.GetServices<ISubtitleProvider>());
            Assert.Equal("Jimakufin", registered.Name);
            Assert.Contains(VideoContentType.Episode, registered.SupportedMediaTypes);
            Assert.Same(registered, Assert.Single(serviceProvider.GetServices<ISubtitleProvider>()));
        }
        finally
        {
            context.Unload();
        }
    }

    public class UnusedLibraryProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            throw new InvalidOperationException("Provider registration should not access the library.");
    }

    [Theory]
    [InlineData("Emby.Jimaku.dll", "bin/{configuration}/netstandard2.0/Emby.Jimaku.dll", "EMBY_TEST_PACKAGE")]
    [InlineData("Jellyfin.Jimakufin.dll", "Jellyfin.Jimakufin/bin/{configuration}/net9.0/Jellyfin.Jimakufin.dll", "JIMAKU_TEST_PACKAGE")]
    public void BothPluginsContainCommonCodeWithoutSharedAssemblyReference(string filename, string relativePath, string packageVariable)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Jimakufin.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var package = Environment.GetEnvironmentVariable(packageVariable);
        using var content = new MemoryStream();
        if (string.IsNullOrEmpty(package))
        {
            relativePath = relativePath.Replace("{configuration}", new DirectoryInfo(AppContext.BaseDirectory).Parent.Name);
            using var input = File.OpenRead(Path.Combine(root.FullName, relativePath));
            input.CopyTo(content);
        }
        else
        {
            using var archive = ZipFile.OpenRead(package);
            Assert.Equal(filename, Assert.Single(archive.Entries).FullName);
            using var input = archive.Entries[0].Open();
            input.CopyTo(content);
        }
        content.Position = 0;
        using var pe = new PEReader(content);
        Assert.True(pe.HasMetadata);
        Assert.True(pe.PEHeaders.CorHeader.Flags.HasFlag(CorFlags.ILOnly));
        var metadata = pe.GetMetadataReader();
        Assert.DoesNotContain(metadata.AssemblyReferences, h => metadata.GetString(metadata.GetAssemblyReference(h).Name) == "Jimaku.Shared");
        Assert.Contains(metadata.TypeDefinitions, h => metadata.GetString(metadata.GetTypeDefinition(h).Name) == "JimakuClient");
    }
}
