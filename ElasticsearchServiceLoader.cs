using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using Bottles.Services;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.StaticFiles;
using Owin;

namespace ElasticsearchService
{
	public class ElasticsearchServiceLoader : IApplicationLoader, IDisposable
	{
	    private Process _process;
	    private IDisposable _kibanaApp;

		public IDisposable Load()
		{
            var configuration = ConfigurationManager.AppSettings["Elasticsearch.Configuration"];

            var location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            using (var configFile = File.Open(string.Format("{0}\\Binaries\\config\\elasticsearch.{1}.yml", location, configuration), FileMode.Open))
            using (var destinationConfig = File.Create(string.Format("{0}\\Binaries\\config\\elasticsearch.yml", location)))
            {
                configFile.CopyTo(destinationConfig);
            }

            var startInfo = new ProcessStartInfo(string.Format("{0}\\Binaries\\bin\\elasticsearch.bat", location))
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = string.Format("{0}\\Binaries\\bin\\", location),
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            var process = Process.Start(startInfo);

            _process = process;

            using (var configFile = File.Open(string.Format("{0}\\Kibana\\config.{1}.js", location, configuration), FileMode.Open))
            using (var destinationConfig = File.Create(string.Format("{0}\\Kibana\\config.js", location)))
            {
                configFile.CopyTo(destinationConfig);
            }

            var fileSystem = new PhysicalFileSystem(string.Format("{0}\\Kibana", location));

		    var options = new FileServerOptions
		    {
		        FileSystem = fileSystem
		    };

		    options.StaticFileOptions.ContentTypeProvider = new CustomContentTypeProvider();

		    var kibanaUrl = ConfigurationManager.AppSettings["Kibana.Url"];

            _kibanaApp = WebApp.Start(kibanaUrl, x => x.UseFileServer(options));

			return this;
		}
		
		public void Dispose()
		{
		    if (_process != null)
		        KillProcessAndChildren(_process.Id);

            _kibanaApp.Dispose();
		}

        private static void KillProcessAndChildren(int pid)
        {
            var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            var moc = searcher.Get();

            foreach (var mo in moc.Cast<ManagementObject>())
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));

            try
            {
                var proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            { /* process already exited */ }
        }
	}
}