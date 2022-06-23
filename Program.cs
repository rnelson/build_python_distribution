using System.Net;
using System.Diagnostics;
using System.IO.Compression;

const string PLATFORM = "amd64";
const string VERSION = "3.10.5";

Console.WriteLine($"Creating a Python {VERSION} ({PLATFORM}) distribution.");

// Create a temporary directory to work out of
var workdir = HelperMethods.GetTemporaryDirectory();
Console.WriteLine($"Working directory: {workdir}");

// Set up a bunch of values that we need later
var portableUrl = $"https://www.python.org/ftp/python/{VERSION}/python-{VERSION}-embed-{PLATFORM}.zip";
var pipScriptUrl = "https://bootstrap.pypa.io/get-pip.py";	
var squishyVersion = VERSION.Substring(0, VERSION.LastIndexOf('.')).Replace(".", string.Empty);
var destinationDirectoryName = $"Python{squishyVersion}";
var destinationDirectory = Path.Combine(workdir, destinationDirectoryName);
var pthFile = Path.Combine(destinationDirectory, $"python{squishyVersion}._pth");
var portableZip = Path.Combine(workdir, $"python-{VERSION}-embed-{PLATFORM}.zip");
var pipScript = Path.Combine(workdir, "get-pip.py");
var pipExecutable = Path.Combine(destinationDirectory, "Scripts", "pip.exe");
var distributionFilename = $"{destinationDirectoryName}.zip";

// Download everything that we need
await HelperMethods.DownloadFile(portableUrl, portableZip);
await HelperMethods.DownloadFile(pipScriptUrl, pipScript);

// Extract the portable distribution
if (Directory.Exists(destinationDirectory)) Directory.Delete(destinationDirectory, true);
Console.WriteLine($"Extracting Python {VERSION} to {destinationDirectoryName}");
ZipFile.ExtractToDirectory(portableZip, destinationDirectory);

// Replace the contents of the _pth file
Console.WriteLine($"Creating a backup of {pthFile}");
File.Move(pthFile, $"{pthFile}.bak");
var pth = new List<string>
{
    "Lib/site-packages",
    $"python{squishyVersion}.zip",
    ".",
    string.Empty,
    "# Uncomment to run site.main() automatically",
    "#import site"
};
Console.WriteLine($"Replacing {pthFile}");
File.Delete(pthFile);
File.WriteAllLines(pthFile, pth);

// Install dependencies
Console.WriteLine("Instalilng dependencies");
HelperMethods.RunPython(destinationDirectory, new List<string> { Path.Combine(workdir, "get-pip.py") });
HelperMethods.Run(destinationDirectory, pipExecutable, new List<string> { "install", "virtualenv" });

// Create the distribution zip and clean up files
Console.WriteLine($"Creating {distributionFilename}");
ZipFile.CreateFromDirectory(destinationDirectory, Path.Combine(workdir, distributionFilename));
if (File.Exists(Path.Combine(@"C:\temp\", distributionFilename))) File.Delete(Path.Combine(@"C:\temp\", distributionFilename));
File.Copy(Path.Combine(workdir, distributionFilename), Path.Combine(@"C:\temp\", distributionFilename));
Console.WriteLine("Cleaning up.");
Directory.Delete(destinationDirectory, true);
File.Delete(pipScript);
File.Delete(portableZip);
Directory.Delete(workdir, true);

Console.WriteLine();
Console.WriteLine();
Console.WriteLine($"Done. Your Python {VERSION} ({PLATFORM}) distribution is available at {Path.Combine(@"C:\temp\", distributionFilename)}");

internal static class HelperMethods
{
    internal static string GetTemporaryDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    internal static async Task DownloadFile(string url, string path)
    {
        Console.WriteLine($"Downloading {url}");

        var client = new HttpClient();
        var response = await client.GetAsync(new Uri(url));

        using var fs = new FileStream(path, FileMode.Create);
        await response.Content.CopyToAsync(fs);
    }

    internal static void RunPython(string pythonDirectory, IEnumerable<string> arguments)
    {
        var interpreter = Path.Combine(pythonDirectory, "python.exe");
        Run(pythonDirectory, interpreter, arguments, true);
    }

    internal static void Run(string workingDirectory, string command, IEnumerable<string> arguments, bool setPythonPath = true)
    {
        var argv = arguments.Aggregate(string.Empty, (c, n) => $"{c} {n}");
        Console.WriteLine($"Executing: {command} {argv}");

        var psi = new ProcessStartInfo(command);
        psi.CreateNoWindow = true;
        psi.Arguments = argv;
        psi.WorkingDirectory = workingDirectory;
        psi.RedirectStandardOutput = true;

        if (setPythonPath)
        {
            Console.WriteLine($"Setting %PYTHONPATH% to {workingDirectory}");
            if (!psi.EnvironmentVariables.ContainsKey("PYTHONPATH"))
                psi.EnvironmentVariables.Add("PYTHONPATH", workingDirectory);
            else
                psi.EnvironmentVariables["PYTHONPATH"] = workingDirectory;
        }
        
        var p = new Process { StartInfo = psi };
        p.Start();
        Console.WriteLine(p.StandardOutput.ReadToEnd());
        p.WaitForExit();
    }
}