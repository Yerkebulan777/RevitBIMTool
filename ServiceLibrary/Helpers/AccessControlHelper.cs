using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;


namespace ServiceLibrary.Helpers
{
    internal static class AccessControlHelper
    {
        public static string GrantFullControlToDirectory(string directoryPath)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);

            StringBuilder output = new StringBuilder();

            _ = output.Append(directoryInfo.FullName);

            SecurityIdentifier adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            foreach (DirectoryInfo currentDirectory in directoryInfo.GetDirectories("*.*", SearchOption.AllDirectories))
            {
                if (!currentDirectory.Name.StartsWith("#") && !currentDirectory.Name.EndsWith("backup"))
                {
                    DirectorySecurity directorySecurity = currentDirectory.GetAccessControl(AccessControlSections.All);
                    AuthorizationRuleCollection rules = directorySecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));
                    bool ruleExists = rules.Cast<FileSystemAccessRule>().Any(rule => rule.IdentityReference == adminSid && rule.FileSystemRights.HasFlag(FileSystemRights.Modify));

                    if (!ruleExists)
                    {
                        try
                        {
                            // Set permissions
                            directorySecurity.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.Modify, AccessControlType.Allow));
                            currentDirectory.SetAccessControl(directorySecurity);
                        }
                        catch (Exception ex)
                        {
                            _ = output.AppendLine($"{currentDirectory.FullName}: {ex.Message}");
                        }
                        finally
                        {
                            foreach (FileInfo fileInfo in currentDirectory.EnumerateFiles("*.rvt", SearchOption.TopDirectoryOnly))
                            {
                                if (!ApplySecurity(fileInfo, adminSid, out string error))
                                {
                                    _ = output.AppendLine(error);
                                }
                            }
                        }
                    }
                }
            }

            return output.ToString();
        }


        private static bool ApplySecurity(FileInfo fileInfo, SecurityIdentifier adminSecurityIdentifier, out string output)
        {
            try
            {
                output = fileInfo.Name;

                FileSecurity fileSecurity = File.GetAccessControl(fileInfo.FullName, AccessControlSections.All);

                if (IsFullControlNeeded(fileSecurity))
                {
                    fileSecurity.AddAccessRule(new FileSystemAccessRule(adminSecurityIdentifier, FileSystemRights.FullControl, AccessControlType.Allow));
                    File.SetAccessControl(fileInfo.FullName, fileSecurity);
                }

                return true;
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return false;
            }
        }


        private static bool IsFullControlNeeded(FileSystemSecurity fileSystemSecurity)
        {
            foreach (FileSystemAccessRule rule in fileSystemSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                AccessControlType acessType = rule.AccessControlType;
                FileSystemRights acessRights = rule.FileSystemRights;

                if (acessType == AccessControlType.Allow && acessType != AccessControlType.Deny)
                {
                    if (acessRights.HasFlag(FileSystemRights.Modify) || acessRights.HasFlag(FileSystemRights.FullControl))
                    {
                        return false;
                    }
                }
            }
            return true;
        }


    }

}
