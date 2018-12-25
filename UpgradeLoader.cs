using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using UtilityService;

namespace UpgradeHost
{
    internal class UpgradeLoader
    {
        private readonly string _upgradeFolder;
        private readonly string _usbPath;
        private readonly string _upgradeTempFolder;
        private readonly string _needBackup;
        private readonly string _clientPath;
        private readonly WizardServiceImpl _wizardService;
        public bool IsInUpgrading { get; private set; }
        private readonly string _title;
        private readonly HostLoaderArgs _arguments;

        private const string DoNotTrunOff = "Don't turn off the Ultrasound system, this will take a while";
        private const string LocalUpgradePath = @"D:\\Upgrade";

        public WizardServiceImpl Wizard { get { return _wizardService; } }

        internal UpgradeLoader(HostLoaderArgs arguments)
        {
            _arguments = arguments ?? new HostLoaderArgs();
            _upgradeFolder = _arguments.UpgradeFolder;
            _usbPath = _arguments.Args[1];
            _upgradeTempFolder = _arguments.Args[2];
            _needBackup = _arguments.Args[3];
            _clientPath = _arguments.Args[4];

            _wizardService = new WizardServiceImpl(OnStartUpgrade);

            _title = Translator.GetValue("Ultrasound System Update");
        }
        public void StartUpgrade()
        {
            _wizardService.Startup();
        }

        private void OnStartUpgrade()
        {
            IsInUpgrading = true;
            string upgradeDescription = string.Empty;
            string stepDes = Translator.GetValue("Working on updates\nPart 4 of 4: Upgrading to new version");
            try
            {
                string destAssembly = Path.Combine(_upgradeFolder, "UpgradeHost.DLL");

                if (!File.Exists(destAssembly)||!DigitalSignatureChecker.VerifySignature(destAssembly))
                {
                    throw new Exception();
                }

                UpgradeProxy proxy=new UpgradeProxy(destAssembly);
                upgradeDescription = proxy.Description();
                _wizardService.UpdateStates(_title, upgradeDescription,stepDes, DoNotTrunOff);
                string message;
                if (!proxy.CanExecute(out message))
                {
                    throw new Exception(message);
                }
                try
                {
                    Thread.Sleep(2000);
                    proxy.Execute(_upgradeFolder,out message);
                }
                catch (Exception e)
                {
                    throw new Exception(e.InnerException!=null?e.InnerException.Message:e.Message);
                }

                try
                {
                    //delete upgrade folder
                    //todo KnowIssue ,UpgradeHost.dll can't be delete,as it has been loaded  into AppDomain
                    Directory.Delete(_upgradeFolder,true);
                }
                catch (Exception e)
                {

                }

                const string shutdownMessage = "Upgrade done successfully.\nSystem will continue automatically after";
                string shutdownAdditionDes = string.Empty;
                var buttons = new WizardButton[1];
                buttons[0] = new WizardButton(WizardButtonEnum.Ok, null, "Continue");
                int timeoutTicks = ResourceManager.GetValue("Upgrade", "TimeoutTicks", 10);
                _wizardService.UpdateStates(_title, upgradeDescription, shutdownMessage, shutdownAdditionDes, -1, true, buttons, timeoutTicks, buttons[0]);

                //Decide to start Main.exe or continue upgrade.
                StartOrContinue();
            }
            catch (Exception e)
            {
                var buttons = new WizardButton[1];
                buttons[0] = new WizardButton(WizardButtonEnum.Ok, null,"Shut Down");
                _wizardService.UpdateStates(_title, upgradeDescription, Translator.GetValue("Upgrade failed.\nSystem will shut down."), e.Message, -1, true, buttons);
                HostManager.ShutDown(RestartType.ShutdownOS);
            }
            finally
            {
                IsInUpgrading = false;
                HostManager.ShutDown(RestartType.None);
            }
        }

        private void StartOrContinue()
        {
            string upgradePackageFile = string.Empty;
            bool isExists = false;

            //Todo: 这个系统version，是否已经是最新版本？如果不是，则需要再判断时再往后跳一个等级的补丁来作为判断依据。
            UpgradePackageService.GetCurrentVersion(out string updatedVersion, out _);

            //Do new multiple upgrade.
            if (!(string.IsNullOrEmpty(_usbPath) || _usbPath == "Null"))
            {
                isExists = IsUpgradeAvailable(updatedVersion, out upgradePackageFile);
            }

            if (isExists)
            {
                StartClient(upgradePackageFile);
            }
            else
            {
                //All upgrades are finished, start system.
                StartSystem();
            }
        }

        private bool IsUpgradeAvailable(string updatedVersion, out string upgradePackageFile)
        {
            bool result = UpgradePackageService.IsUpgradePackageExists(_usbPath, updatedVersion, out string zipFilePath);
            if (result)
            {
                UpgradePackageService.ReleaseToLocal(zipFilePath);
                //Detailed check, check the version of Resource & dat file.
                if (UpgradePackageService.CheckUpgradeDetail(LocalUpgradePath))
                {
                    //Upgrade package found.
                    upgradePackageFile = Path.Combine(LocalUpgradePath, UpgradePackageService.GetVersion(zipFilePath) + ".dat");
                    return true;
                }

                //Try to find a higher version upgrade package.
                updatedVersion = UpgradePackageService.GetVersion(zipFilePath);
                if (IsUpgradeAvailable(updatedVersion, out upgradePackageFile))
                {
                    return true;
                }
            }
            else
            {
                upgradePackageFile = string.Empty;
                return false;
            }

            return false;
        }

        private void StartClient(string upgradePackageFile)
        {
            string arguments = string.Format("\"{0}\"\"{1}\"\"{2}\"\"{3}\"", upgradePackageFile,
                _upgradeTempFolder, _needBackup, _usbPath);
            string workingDirectory = Path.GetDirectoryName(_clientPath);

            //Start new client.exe.
            Process p = new Process();
            p.StartInfo.FileName = _clientPath;
            p.StartInfo.CreateNoWindow = false;
            p.StartInfo.WorkingDirectory = workingDirectory;
            p.StartInfo.UseShellExecute = true;
            p.StartInfo.Arguments = arguments;

            p.Start();
        }

        private static void StartSystem()
        {
            const string workingDirectory = @"C:\App\Shipped\bin\";
            const string fileName = workingDirectory + ".MainMonitor.exe";
            if (!File.Exists(fileName))
            {
                throw new Exception(string.Format(Translator.GetValue("File not Exists: '{0}'."), fileName));
            }

            if (!DigitalSignatureChecker.VerifySignature(fileName))
            {
                throw new Exception(string.Format(Translator.GetValue("Failed to verify digital signature of the module '{0}'."), fileName));
            }

            new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false
                }
            }.Start();
        }
    }
}