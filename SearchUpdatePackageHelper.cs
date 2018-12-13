using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpgradeTool
{
    internal static class SearchUpdatePackageHelper
    {
        /// <summary>
        /// //Figure out if upgrade is proper. If ok, send out zip file path.
        /// </summary>
        /// <param name="usbPath">The usb path.</param>
        /// <param name="currentVersion">The string of current version.</param>
        /// <param name="zipFilePath">Send out the update package zip file path.</param>
        /// <returns></returns>
        internal static bool IsUpgradePackageExists(string usbPath, string currentVersion, out string zipFilePath)
        {
            var current = currentVersion.Split('.', '(', ')');

            //Find all zip files in USB.
            var zipFiles = VinnoDirectory.GetFiles(usbPath, "*.zip");
            if (zipFiles.Length == 0)
            {
                zipFilePath = string.Empty;
                return false;
            }

            //Get first and last update package version.
            var front = Path.GetFileNameWithoutExtension(zipFiles[0])?.Split('.', '(', ')');
            var back = Path.GetFileNameWithoutExtension(zipFiles.Last())?.Split('.', '(', ')');
            if (front == null || back == null)
            {
                zipFilePath = string.Empty;
                return false;
            }

            switch (current.Length)
            {
                case 3:
                    {
                        int currentSum = int.Parse(current[0]) * 100 + int.Parse(current[1]) * 10 +
                                         int.Parse(current[2]) * 1;
                        int frontSum = int.Parse(front[0]) * 100 + int.Parse(front[1]) * 10 +
                                       int.Parse(front[2]) * 1;
                        int backSum = int.Parse(back[0]) * 100 + int.Parse(back[1]) * 10 +
                                      int.Parse(back[2]) * 1;

                        if (currentSum <= frontSum)
                        {
                            switch (front.Length)
                            {
                                case 3:
                                    if (currentSum < frontSum)
                                    {
                                        zipFilePath = zipFiles[0];
                                        return true;
                                    }
                                    break;
                                case 4:
                                    zipFilePath = zipFiles[0];
                                    return true;
                                default:
                                    zipFilePath = string.Empty;
                                    return false;
                            }
                        }

                        if (frontSum <= currentSum && currentSum <= backSum)
                        {
                            for (int i = 0; i < zipFiles.Length - 1; i++)
                            {
                                var position = Path.GetFileNameWithoutExtension(zipFiles[i])?.Split('.', '(', ')');
                                var positionNext = Path.GetFileNameWithoutExtension(zipFiles[i + 1])?.Split('.', '(', ')');
                                if (position == null || positionNext == null) continue;
                                int positionSum = int.Parse(position[0]) * 100 + int.Parse(position[1]) * 10 +
                                                  int.Parse(position[2]) * 1;

                                int positionNextSum = int.Parse(positionNext[0]) * 100 + int.Parse(positionNext[1]) * 10 +
                                                      int.Parse(positionNext[2]) * 1;
                                switch (position.Length)
                                {
                                    case 3:
                                        {
                                            switch (positionNext.Length)
                                            {
                                                case 3:
                                                    if (positionSum < currentSum && currentSum < positionNextSum)
                                                    {
                                                        zipFilePath = zipFiles[i + 1];
                                                        return true;
                                                    }
                                                    break;
                                                case 4:
                                                    if (positionSum < currentSum && currentSum <= positionNextSum)
                                                    {
                                                        zipFilePath = zipFiles[i + 1];
                                                        return true;
                                                    }
                                                    break;
                                                default:
                                                    zipFilePath = string.Empty;
                                                    return false;
                                            }
                                            break;
                                        }
                                    case 4:
                                        {
                                            switch (positionNext.Length)
                                            {
                                                case 3:
                                                    if (positionSum < currentSum && currentSum < positionNextSum)
                                                    {
                                                        zipFilePath = zipFiles[i + 1];
                                                        return true;
                                                    }
                                                    break;
                                                case 4:
                                                    if (positionSum < currentSum && currentSum < positionNextSum)
                                                    {
                                                        zipFilePath = zipFiles[i + 1];
                                                        return true;
                                                    }
                                                    break;
                                                default:
                                                    {
                                                        zipFilePath = string.Empty;
                                                        return false;
                                                    }
                                            }
                                            break;
                                        }

                                    default:
                                        zipFilePath = string.Empty;
                                        return false;
                                }
                            }
                        }

                        if (currentSum >= backSum)
                        {
                            switch (back.Length)
                            {
                                case 3:
                                    zipFilePath = string.Empty;
                                    return false;
                                case 4:
                                    if (currentSum == backSum)
                                    {
                                        zipFilePath = zipFiles.Last();
                                        return true;
                                    }
                                    break;
                                default:
                                    zipFilePath = string.Empty;
                                    return false;
                            }

                            zipFilePath = string.Empty;
                            return false;
                        }

                        break;
                    }
                case 4:
                    {
                        int currentSum = int.Parse(current[0]) * 100 + int.Parse(current[1]) * 10 +
                                         int.Parse(current[2]) * 1;
                        int frontSum = int.Parse(front[0]) * 100 + int.Parse(front[1]) * 10 +
                                       int.Parse(front[2]) * 1;
                        int backSum = int.Parse(back[0]) * 100 + int.Parse(back[1]) * 10 +
                                      int.Parse(back[2]) * 1;

                        if (currentSum <= frontSum)
                        {
                            switch (front.Length)
                            {
                                case 3:
                                    if (currentSum < frontSum)
                                    {
                                        zipFilePath = zipFiles[0];
                                        return true;
                                    }
                                    break;
                                case 4:
                                    if (currentSum < frontSum)
                                    {
                                        zipFilePath = zipFiles[0];
                                        return true;
                                    }
                                    if (currentSum == frontSum && string.CompareOrdinal(front[3], current[3]) > 0)
                                    {
                                        zipFilePath = zipFiles[0];
                                        return true;
                                    }

                                    break;
                                default:
                                    zipFilePath = string.Empty;
                                    return false;
                            }
                        }

                        if (frontSum <= currentSum && currentSum <= backSum)
                        {
                            for (int i = 0; i < zipFiles.Length - 1; i++)
                            {
                                var position = Path.GetFileNameWithoutExtension(zipFiles[i])?.Split('.', '(', ')');
                                var positionNext = Path.GetFileNameWithoutExtension(zipFiles[i + 1])?.Split('.', '(', ')');
                                if (position == null || positionNext == null) continue;
                                int positionSum = int.Parse(position[0]) * 100 + int.Parse(position[1]) * 10 +
                                                  int.Parse(position[2]) * 1;
                                int positionNextSum = int.Parse(positionNext[0]) * 100 + int.Parse(positionNext[1]) * 10 +
                                                      int.Parse(positionNext[2]) * 1;
                                switch (position.Length)
                                {
                                    case 3:
                                        {
                                            switch (positionNext.Length)
                                            {
                                                case 3:
                                                    if (positionSum <= currentSum && currentSum < positionNextSum)
                                                    {
                                                        zipFilePath = zipFiles[i + 1];
                                                        return true;
                                                    }
                                                    break;
                                                case 4:
                                                    {
                                                        if (positionSum <= currentSum && currentSum <= positionNextSum)
                                                        {
                                                            if (currentSum < positionNextSum)
                                                            {
                                                                zipFilePath = zipFiles[i + 1];
                                                                return true;
                                                            }

                                                            if ((currentSum == positionNextSum) && string.CompareOrdinal(current[3], positionNext[3]) < 0)
                                                            {
                                                                zipFilePath = zipFiles[i + 1];
                                                                return true;
                                                            }
                                                        }
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        zipFilePath = string.Empty;
                                                        return false;
                                                    }
                                            }
                                            break;
                                        }
                                    case 4:
                                        {
                                            if (positionNext.Length == 3)
                                            {
                                                if (currentSum > positionSum && currentSum < positionNextSum)
                                                {
                                                    zipFilePath = zipFiles[i + 1];
                                                    return true;
                                                }

                                                if (currentSum == positionSum && string.CompareOrdinal(current[3], position[3]) > 0 && currentSum < positionNextSum)
                                                {
                                                    zipFilePath = zipFiles[i + 1];
                                                    return true;
                                                }
                                            }

                                            if (positionNext.Length == 4)
                                            {
                                                if (currentSum > positionSum && currentSum < positionNextSum)
                                                {
                                                    zipFilePath = zipFiles[i + 1];
                                                    return true;
                                                }

                                                if (currentSum == positionSum && string.CompareOrdinal(current[3], position[3]) > 0 && currentSum < positionNextSum)
                                                {
                                                    zipFilePath = zipFiles[i + 1];
                                                    return true;
                                                }

                                                if (currentSum > positionSum && currentSum == positionNextSum && string.CompareOrdinal(current[3], positionNext[3]) < 0)
                                                {
                                                    zipFilePath = zipFiles[i + 1];
                                                    return true;
                                                }

                                                if (currentSum == positionSum && currentSum == positionNextSum &&
                                                    string.CompareOrdinal(current[3], position[3]) > 0 &&
                                                    string.CompareOrdinal(positionNext[3], current[3]) > 0)
                                                {
                                                    zipFilePath = zipFiles[i + 1];
                                                    return true;
                                                }
                                            }

                                            break;
                                        }
                                    default:
                                        {
                                            zipFilePath = string.Empty;
                                            return false;
                                        }
                                }
                            }
                        }

                        if (currentSum >= backSum)
                        {
                            switch (back.Length)
                            {
                                case 3:
                                    zipFilePath = string.Empty;
                                    return false;
                                case 4:
                                    if (currentSum > backSum)
                                    {
                                        zipFilePath = string.Empty;
                                        return false;
                                    }
                                    if (currentSum == backSum && string.CompareOrdinal(back[3], current[3]) > 0)
                                    {
                                        zipFilePath = zipFiles.Last();
                                        return true;
                                    }

                                    zipFilePath = string.Empty;
                                    return false;
                                default:
                                    {
                                        zipFilePath = string.Empty;
                                        return false;
                                    }
                            }
                        }
                        break;
                    }

                default:
                    {
                        zipFilePath = string.Empty;
                        return false;
                    } 
            }

            zipFilePath = string.Empty;
            return false;
        }
    }
}
