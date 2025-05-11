using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Samco_HSE_Manager.Base;

namespace Samco_HSE_Manager
{
    public sealed class PhysicalFileProvider : PhysicalFileProviderBase
    {
        private string contentRootPath;
        private string[] allowedExtension = { "*" };
        AccessDetails AccessDetails = new();
        private string rootName = string.Empty;
        private string hostPath;
        private string hostName;
        private string accessMessage = string.Empty;

        public void RootFolder(string name)
        {
            contentRootPath = name;
            hostName = new Uri(contentRootPath).Host;
            if (!string.IsNullOrEmpty(hostName))
            {
                hostPath = Path.DirectorySeparatorChar + hostName + Path.DirectorySeparatorChar + contentRootPath.Substring((contentRootPath.ToLower().IndexOf(hostName) + hostName.Length + 1));
            }
        }

        public void SetRules(AccessDetails details)
        {
            AccessDetails = details;
            var root = new DirectoryInfo(contentRootPath);
            rootName = root.Name;
        }

        public FileManagerResponse GetFiles(string? path, bool showHiddenItems, params FileManagerDirectoryContent[] data)
        {
            var readResponse = new FileManagerResponse();
            try
            {
                path ??= string.Empty;
                var fullPath = (contentRootPath + path);
                fullPath = fullPath.Replace("../", "");
                var directory = new DirectoryInfo(fullPath);
                var extensions = allowedExtension;
                var cwd = new FileManagerDirectoryContent();
                var rootPath = string.IsNullOrEmpty(hostPath) ? contentRootPath : new DirectoryInfo(hostPath).FullName;
                var parentPath = string.IsNullOrEmpty(hostPath) ? directory.Parent.FullName : new DirectoryInfo(hostPath + (path != "/" ? path : "")).Parent.FullName;
                cwd.Name = string.IsNullOrEmpty(hostPath) ? directory.Name : new DirectoryInfo(hostPath + path).Name;
                cwd.Size = 0;
                cwd.IsFile = false;
                cwd.DateModified = directory.LastWriteTime;
                cwd.DateCreated = directory.CreationTime;
                cwd.HasChild = CheckChild(directory.FullName);
                cwd.Type = directory.Extension;
                cwd.FilterPath = GetRelativePath(rootPath, parentPath + Path.DirectorySeparatorChar);
                cwd.Permission = GetPathPermission(path);
                readResponse.CWD = cwd;
                if (!hasAccess(directory.FullName) || (cwd.Permission != null && !cwd.Permission.Read))
                {
                    readResponse.Files = null;
                    accessMessage = cwd.Permission.Message;
                    throw new UnauthorizedAccessException("'" + cwd.Name + "' is not accessible. You need permission to perform the read action.");
                }
                readResponse.Files = ReadDirectories(directory, extensions, showHiddenItems, data);
                readResponse.Files = readResponse.Files.Concat(ReadFiles(directory, extensions, showHiddenItems, data));
                return readResponse;
            }
            catch (Exception e)
            {
                var er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                readResponse.Error = er;
                return readResponse;
            }
        }

        private IEnumerable<FileManagerDirectoryContent> ReadFiles(DirectoryInfo directory, string[] extensions, bool showHiddenItems, params FileManagerDirectoryContent[] data)
        {
            try
            {
                var readFiles = new FileManagerResponse();
                if (!showHiddenItems)
                {
                    var files = extensions.SelectMany(directory.GetFiles).Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                            .Select(file => new FileManagerDirectoryContent
                            {
                                Name = file.Name,
                                IsFile = true,
                                Size = file.Length,
                                DateModified = file.LastWriteTime,
                                DateCreated = file.CreationTime,
                                HasChild = false,
                                Type = file.Extension,
                                FilterPath = GetRelativePath(contentRootPath, directory.FullName),
                                Permission = GetPermission(directory.FullName, file.Name, true)
                            });
                    readFiles.Files = files;
                }
                else
                {
                    IEnumerable<FileManagerDirectoryContent> files = extensions.SelectMany(directory.GetFiles)
                            .Select(file => new FileManagerDirectoryContent
                            {
                                Name = file.Name,
                                IsFile = true,
                                Size = file.Length,
                                DateModified = file.LastWriteTime,
                                DateCreated = file.CreationTime,
                                HasChild = false,
                                Type = file.Extension,
                                FilterPath = GetRelativePath(contentRootPath, directory.FullName),
                                Permission = GetPermission(directory.FullName, file.Name, true)
                            });
                    readFiles.Files = (IEnumerable<FileManagerDirectoryContent>)files;
                }
                return readFiles.Files;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private string GetRelativePath(string rootPath, string fullPath)
        {
            if (!String.IsNullOrEmpty(rootPath) && !String.IsNullOrEmpty(fullPath))
            {
                DirectoryInfo rootDirectory;
                if (!string.IsNullOrEmpty(hostName))
                {
                    if (rootPath.Contains(hostName) || rootPath.ToLower().Contains(hostName) || rootPath.ToUpper().Contains(hostName))
                    {
                        rootPath = rootPath.Substring(rootPath.IndexOf(hostName, StringComparison.CurrentCultureIgnoreCase) + hostName.Length);
                    }
                    if (fullPath.Contains(hostName) || fullPath.ToLower().Contains(hostName) || fullPath.ToUpper().Contains(hostName))
                    {
                        fullPath = fullPath.Substring(fullPath.IndexOf(hostName, StringComparison.CurrentCultureIgnoreCase) + hostName.Length);
                    }
                    rootDirectory = new DirectoryInfo(rootPath);
                    fullPath = new DirectoryInfo(fullPath).FullName;
                    rootPath = new DirectoryInfo(rootPath).FullName;
                }
                else
                {
                    rootDirectory = new DirectoryInfo(rootPath);
                }
                if (rootDirectory.FullName.Substring(rootDirectory.FullName.Length - 1) == Path.DirectorySeparatorChar.ToString())
                {
                    if (fullPath.Contains(rootDirectory.FullName))
                    {
                        return fullPath.Substring(rootPath.Length - 1);
                    }
                }
                else if (fullPath.Contains(rootDirectory.FullName + Path.DirectorySeparatorChar))
                {
                    return Path.DirectorySeparatorChar + fullPath.Substring(rootPath.Length + 1);
                }
            }
            return String.Empty;
        }


        private IEnumerable<FileManagerDirectoryContent> ReadDirectories(DirectoryInfo directory, string[] extensions, bool showHiddenItems, params FileManagerDirectoryContent[] data)
        {
            var readDirectory = new FileManagerResponse();
            try
            {
                if (!showHiddenItems)
                {
                    var directories = directory.GetDirectories().Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                            .Select(subDirectory => new FileManagerDirectoryContent
                            {
                                Name = subDirectory.Name,
                                Size = 0,
                                IsFile = false,
                                DateModified = subDirectory.LastWriteTime,
                                DateCreated = subDirectory.CreationTime,
                                HasChild = CheckChild(subDirectory.FullName),
                                Type = subDirectory.Extension,
                                FilterPath = GetRelativePath(contentRootPath, directory.FullName),
                                Permission = GetPermission(directory.FullName, subDirectory.Name, false)
                            });
                    readDirectory.Files = directories;
                }
                else
                {
                    IEnumerable<FileManagerDirectoryContent> directories = directory.GetDirectories().Select(subDirectory => new FileManagerDirectoryContent
                    {
                        Name = subDirectory.Name,
                        Size = 0,
                        IsFile = false,
                        DateModified = subDirectory.LastWriteTime,
                        DateCreated = subDirectory.CreationTime,
                        HasChild = CheckChild(subDirectory.FullName),
                        Type = subDirectory.Extension,
                        FilterPath = GetRelativePath(contentRootPath, directory.FullName),
                        Permission = GetPermission(directory.FullName, subDirectory.Name, false)
                    });
                    readDirectory.Files = directories;
                }
                return readDirectory.Files;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        public FileManagerResponse Create(string path, string name, params FileManagerDirectoryContent[] data)
        {
            var createResponse = new FileManagerResponse();
            try
            {
                var PathPermission = GetPathPermission(path);
                if (PathPermission != null && (!PathPermission.Read || !PathPermission.WriteContents))
                {
                    accessMessage = PathPermission.Message;
                    throw new UnauthorizedAccessException("'" + getFileNameFromPath(rootName + path) + "' is not accessible. You need permission to perform the writeContents action.");
                }

                var newDirectoryPath = Path.Combine(contentRootPath + path, name);
                newDirectoryPath = newDirectoryPath.Replace("../", "");

                var directoryExist = Directory.Exists(newDirectoryPath);

                if (directoryExist)
                {
                    var exist = new DirectoryInfo(newDirectoryPath);
                    var er = new ErrorDetails();
                    er.Code = "400";
                    er.Message = "A file or folder with the name " + exist.Name.ToString() + " already exists.";
                    createResponse.Error = er;

                    return createResponse;
                }

                var physicalPath = GetPath(path);
                Directory.CreateDirectory(newDirectoryPath);
                var directory = new DirectoryInfo(newDirectoryPath);
                var CreateData = new FileManagerDirectoryContent();
                CreateData.Name = directory.Name;
                CreateData.IsFile = false;
                CreateData.Size = 0;
                CreateData.DateModified = directory.LastWriteTime;
                CreateData.DateCreated = directory.CreationTime;
                CreateData.HasChild = CheckChild(directory.FullName);
                CreateData.Type = directory.Extension;
                CreateData.Permission = GetPermission(physicalPath, directory.Name, false);
                FileManagerDirectoryContent[] newData = { CreateData };
                createResponse.Files = newData;
                return createResponse;
            }
            catch (Exception e)
            {
                var er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                createResponse.Error = er;
                return createResponse;
            }
        }
        public FileManagerResponse Details(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            var getDetailResponse = new FileManagerResponse();
            var detailFiles = new FileDetails();
            try
            {
                if (names.Length == 0 || names.Length == 1)
                {
                    if (path == null) { path = string.Empty; };
                    var fullPath = "";
                    if (names.Length == 0)
                    {
                        fullPath = (contentRootPath + path.Substring(0, path.Length - 1));
                    }
                    else if (string.IsNullOrEmpty(names[0]))
                    {
                        fullPath = (contentRootPath + path);
                    }
                    else
                    {
                        fullPath = Path.Combine(contentRootPath + path, names[0]);
                        fullPath = fullPath.Replace("../", "");
                    }
                    var physicalPath = GetPath(path);
                    var directory = new DirectoryInfo(fullPath);
                    var info = new FileInfo(fullPath);
                    var fileDetails = new FileDetails();
                    var baseDirectory = new DirectoryInfo(string.IsNullOrEmpty(hostPath) ? contentRootPath : hostPath);
                    fileDetails.Name = info.Name == "" ? directory.Name : info.Name;
                    fileDetails.IsFile = (File.GetAttributes(fullPath) & FileAttributes.Directory) != FileAttributes.Directory;
                    fileDetails.Size = (File.GetAttributes(fullPath) & FileAttributes.Directory) != FileAttributes.Directory ? byteConversion(info.Length).ToString() : byteConversion(GetDirectorySize(new DirectoryInfo(fullPath), 0)).ToString();
                    fileDetails.Created = info.CreationTime;
                    fileDetails.Location = GetRelativePath(string.IsNullOrEmpty(hostName) ? baseDirectory.Parent.FullName : baseDirectory.Parent.FullName + Path.DirectorySeparatorChar, info.FullName).Substring(1);
                    fileDetails.Modified = info.LastWriteTime;
                    fileDetails.Permission = GetPermission(physicalPath, fileDetails.Name, fileDetails.IsFile);
                    detailFiles = fileDetails;
                }
                else
                {
                    var isVariousFolders = false;
                    var relativePath = "";
                    var previousPath = "";
                    var previousName = "";
                    var fileDetails = new FileDetails();
                    fileDetails.Size = "0";
                    var baseDirectory = new DirectoryInfo(string.IsNullOrEmpty(hostPath) ? contentRootPath : hostPath);
                    var parentPath = baseDirectory.Parent.FullName;
                    var baseDirectoryParentPath = string.IsNullOrEmpty(hostName) ? parentPath : parentPath + Path.DirectorySeparatorChar;
                    for (var i = 0; i < names.Length; i++)
                    {
                        var fullPath = "";
                        if (names[i] == null)
                        {
                            fullPath = (contentRootPath + path);
                        }
                        else
                        {
                            fullPath = Path.Combine(contentRootPath + path, names[i]);
                            fullPath = fullPath.Replace("../", "");
                        }
                        var info = new FileInfo(fullPath);
                        fileDetails.Name = previousName == "" ? previousName = data[i].Name : previousName = previousName + ", " + data[i].Name;
                        fileDetails.Size = (long.Parse(fileDetails.Size) + (((File.GetAttributes(fullPath) & FileAttributes.Directory) != FileAttributes.Directory) ? info.Length : GetDirectorySize(new DirectoryInfo(fullPath), 0))).ToString();
                        relativePath = GetRelativePath(baseDirectoryParentPath, info.Directory.FullName);
                        previousPath = previousPath == "" ? relativePath : previousPath;
                        if (previousPath == relativePath && !isVariousFolders)
                        {
                            previousPath = relativePath;
                        }
                        else
                        {
                            isVariousFolders = true;
                        }
                    }
                    fileDetails.Size = byteConversion(long.Parse(fileDetails.Size)).ToString();
                    fileDetails.MultipleFiles = true;
                    detailFiles = fileDetails;
                }
                getDetailResponse.Details = detailFiles;
                return getDetailResponse;
            }
            catch (Exception e)
            {
                var er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                getDetailResponse.Error = er;
                return getDetailResponse;
            }
        }

        public FileManagerResponse Delete(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            var DeleteResponse = new FileManagerResponse();
            List<FileManagerDirectoryContent> removedFiles = new();
            try
            {
                var physicalPath = GetPath(path);
                var result = String.Empty;
                for (var i = 0; i < names.Length; i++)
                {
                    var IsFile = !IsDirectory(physicalPath, names[i]);
                    var permission = GetPermission(physicalPath, names[i], IsFile);
                    if (permission != null && (!permission.Read || !permission.Write))
                    {
                        accessMessage = permission.Message;
                        throw new UnauthorizedAccessException("'" + getFileNameFromPath(rootName + path + names[i]) + "' is not accessible.  you need permission to perform the write action.");
                    }
                }
                FileManagerDirectoryContent removingFile;
                for (var i = 0; i < names.Length; i++)
                {
                    var fullPath = Path.Combine((contentRootPath + path), names[i]);
                    fullPath = fullPath.Replace("../", "");
                    var directory = new DirectoryInfo(fullPath);
                    if (!string.IsNullOrEmpty(names[i]))
                    {
                        var attr = File.GetAttributes(fullPath);
                        removingFile = GetFileDetails(fullPath);
                        //detect whether its a directory or file
                        if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                        {
                            result = DeleteDirectory(fullPath);
                        }
                        else
                        {
                            try
                            {
                                File.Delete(fullPath);
                            }
                            catch (Exception e)
                            {
                                if (e.GetType().Name == "UnauthorizedAccessException")
                                {
                                    result = fullPath;
                                }
                                else
                                {
                                    throw e;
                                }
                            }
                        }
                        if (result != String.Empty)
                        {
                            break;

                        }
                        removedFiles.Add(removingFile);
                    }
                    else
                    {
                        throw new ArgumentNullException("name should not be null");
                    }
                }
                DeleteResponse.Files = removedFiles;
                if (result != String.Empty)
                {
                    var deniedPath = result.Substring(contentRootPath.Length);
                    var er = new ErrorDetails();
                    er.Message = "'" + getFileNameFromPath(deniedPath) + "' is not accessible.  you need permission to perform the write action.";
                    er.Code = "401";
                    if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                    DeleteResponse.Error = er;
                    return DeleteResponse;
                }
                else
                {
                    return DeleteResponse;
                }
            }
            catch (Exception e)
            {
                var er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                DeleteResponse.Error = er;
                return DeleteResponse;
            }
        }

        public FileManagerResponse Rename(string path, string name, string newName, bool replace = false, bool showFileExtension = true, params FileManagerDirectoryContent[] data)
        {
            var renameResponse = new FileManagerResponse();
            try
            {
                var physicalPath = GetPath(path);
                if (!showFileExtension)
                {
                    name = name + data[0].Type;
                    newName = newName + data[0].Type;
                }
                var IsFile = !IsDirectory(physicalPath, name);
                var permission = GetPermission(physicalPath, name, IsFile);
                if (permission != null && (!permission.Read || !permission.Write))
                {
                    accessMessage = permission.Message;
                    throw new UnauthorizedAccessException();
                }

                var tempPath = (contentRootPath + path);
                tempPath = tempPath.Replace("../", "");
                var oldPath = Path.Combine(tempPath, name);
                var newPath = Path.Combine(tempPath, newName);
                var attr = File.GetAttributes(oldPath);

                var info = new FileInfo(oldPath);
                var isFile = (File.GetAttributes(oldPath) & FileAttributes.Directory) != FileAttributes.Directory;
                if (isFile)
                {
                    if (File.Exists(newPath) && !oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var exist = new FileInfo(newPath);
                        var er = new ErrorDetails();
                        er.Code = "400";
                        er.Message = "Cannot rename " + exist.Name.ToString() + " to " + newName + ": destination already exists.";
                        renameResponse.Error = er;
                        return renameResponse;
                    }
                    info.MoveTo(newPath);
                }
                else
                {
                    var directoryExist = Directory.Exists(newPath);
                    if (directoryExist && !oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var exist = new DirectoryInfo(newPath);
                        var er = new ErrorDetails();
                        er.Code = "400";
                        er.Message = "Cannot rename " + exist.Name.ToString() + " to " + newName + ": destination already exists.";
                        renameResponse.Error = er;

                        return renameResponse;
                    }
                    else if (oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        tempPath = Path.Combine(tempPath + "Syncfusion_TempFolder");
                        Directory.Move(oldPath, tempPath);
                        Directory.Move(tempPath, newPath);
                    }
                    else
                    {
                        Directory.Move(oldPath, newPath);
                    }
                }
                FileManagerDirectoryContent[] addedData = {
                        GetFileDetails(newPath)
                    };
                renameResponse.Files = addedData;
                return renameResponse;
            }
            catch (Exception e)
            {
                var er = new ErrorDetails();
                er.Message = (e.GetType().Name == "UnauthorizedAccessException") ? "'" + getFileNameFromPath(rootName + path + name) + "' is not accessible. You need permission to perform the write action." : e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                renameResponse.Error = er;
                return renameResponse;
            }
        }

        public FileManagerResponse Copy(string path, string targetPath, string[] names, string[] renameFiles, FileManagerDirectoryContent targetData, params FileManagerDirectoryContent[] data)
        {
            var copyResponse = new FileManagerResponse();
            try
            {
                var result = String.Empty;
                if (renameFiles == null)
                {
                    renameFiles = new string[0];
                }
                var physicalPath = GetPath(path);
                for (var i = 0; i < names.Length; i++)
                {
                    var IsFile = !IsDirectory(physicalPath, names[i]);
                    var permission = GetPermission(physicalPath, names[i], IsFile);
                    if (permission != null && (!permission.Read || !permission.Copy))
                    {
                        accessMessage = permission.Message;
                        throw new UnauthorizedAccessException("'" + getFileNameFromPath(rootName + path + names[i]) + "' is not accessible. You need permission to perform the copy action.");
                    }
                }
                var PathPermission = GetPathPermission(targetPath);
                if (PathPermission != null && (!PathPermission.Read || !PathPermission.WriteContents))
                {
                    accessMessage = PathPermission.Message;
                    throw new UnauthorizedAccessException("'" + getFileNameFromPath(rootName + targetPath) + "' is not accessible. You need permission to perform the writeContents action.");
                }


                List<string> existFiles = new();
                List<string> missingFiles = new();
                List<FileManagerDirectoryContent> copiedFiles = new();
                var tempPath = path;
                for (var i = 0; i < names.Length; i++)
                {
                    var fullname = names[i];
                    var name = names[i].LastIndexOf("/");
                    if (name >= 0)
                    {
                        path = tempPath + names[i].Substring(0, name + 1);
                        names[i] = names[i].Substring(name + 1);
                    }
                    else
                    {
                        path = tempPath;
                    }
                    var itemPath = Path.Combine(contentRootPath + path, names[i]);
                    itemPath = itemPath.Replace("../", "");
                    if (Directory.Exists(itemPath) || File.Exists(itemPath))
                    {
                        if ((File.GetAttributes(itemPath) & FileAttributes.Directory) == FileAttributes.Directory)
                        {
                            var directoryName = names[i];
                            var oldPath = Path.Combine(contentRootPath + path, directoryName);
                            oldPath = oldPath.Replace("../", "");
                            var newPath = Path.Combine(contentRootPath + targetPath, directoryName);
                            newPath = newPath.Replace("../", "");
                            var exist = Directory.Exists(newPath);
                            if (exist)
                            {
                                var index = -1;
                                if (renameFiles.Length > 0)
                                {
                                    index = Array.FindIndex(renameFiles, row => row.Contains(directoryName));
                                }
                                if ((newPath == oldPath) || (index != -1))
                                {
                                    newPath = DirectoryRename(newPath);
                                    result = DirectoryCopy(oldPath, newPath);
                                    if (result != String.Empty) { break; }
                                    var detail = GetFileDetails(newPath);
                                    detail.PreviousName = names[i];
                                    copiedFiles.Add(detail);
                                }
                                else
                                {
                                    existFiles.Add(fullname);
                                }
                            }
                            else
                            {
                                result = DirectoryCopy(oldPath, newPath);
                                if (result != String.Empty) { break; }
                                var detail = GetFileDetails(newPath);
                                detail.PreviousName = names[i];
                                copiedFiles.Add(detail);
                            }
                        }
                        else
                        {
                            var fileName = names[i];
                            var oldPath = Path.Combine(contentRootPath + path, fileName);
                            oldPath = oldPath.Replace("../", "");
                            var newPath = Path.Combine(contentRootPath + targetPath, fileName);
                            newPath = newPath.Replace("../", "");
                            var fileExist = File.Exists(newPath);
                            try
                            {

                                if (fileExist)
                                {
                                    var index = -1;
                                    if (renameFiles.Length > 0)
                                    {
                                        index = Array.FindIndex(renameFiles, row => row.Contains(fileName));
                                    }
                                    if ((newPath == oldPath) || (index != -1))
                                    {
                                        newPath = FileRename(newPath, fileName);
                                        File.Copy(oldPath, newPath);
                                        var detail = GetFileDetails(newPath);
                                        detail.PreviousName = names[i];
                                        copiedFiles.Add(detail);
                                    }
                                    else
                                    {
                                        existFiles.Add(fullname);
                                    }
                                }
                                else
                                {
                                    if (renameFiles.Length > 0)
                                    {
                                        File.Delete(newPath);
                                    }
                                    File.Copy(oldPath, newPath);
                                    var detail = GetFileDetails(newPath);
                                    detail.PreviousName = names[i];
                                    copiedFiles.Add(detail);
                                }
                            }
                            catch (Exception e)
                            {
                                if (e.GetType().Name == "UnauthorizedAccessException")
                                {
                                    result = newPath;
                                    break;
                                }
                                else
                                {
                                    throw e;
                                }
                            }
                        }
                    }
                    else
                    {
                        missingFiles.Add(names[i]);
                    }
                }
                copyResponse.Files = copiedFiles;
                if (result != String.Empty)
                {
                    var deniedPath = result.Substring(contentRootPath.Length);
                    var er = new ErrorDetails();
                    er.Message = "'" + getFileNameFromPath(deniedPath) + "' is not accessible. You need permission to perform the copy action.";
                    er.Code = "401";
                    copyResponse.Error = er;
                    return copyResponse;
                }

                if (existFiles.Count > 0)
                {
                    var er = new ErrorDetails();
                    er.FileExists = existFiles;
                    er.Code = "400";
                    er.Message = "File Already Exists";
                    copyResponse.Error = er;
                }
                if (missingFiles.Count == 0)
                {
                    return copyResponse;
                }
                else
                {
                    var namelist = missingFiles[0];
                    for (var k = 1; k < missingFiles.Count; k++)
                    {
                        namelist = namelist + ", " + missingFiles[k];
                    }
                    throw new FileNotFoundException(namelist + " not found in given location.");
                }
            }
            catch (Exception e)
            {
                var er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                er.FileExists = copyResponse.Error?.FileExists;
                copyResponse.Error = er;
                return copyResponse;
            }
        }

        public FileManagerResponse Move(string path, string targetPath, string[] names, string[] renameFiles, FileManagerDirectoryContent targetData, params FileManagerDirectoryContent[] data)
        {
            var moveResponse = new FileManagerResponse();
            try
            {
                var result = String.Empty;
                if (renameFiles == null)
                {
                    renameFiles = new string[0];
                }
                var physicalPath = GetPath(path);
                for (var i = 0; i < names.Length; i++)
                {
                    var IsFile = !IsDirectory(physicalPath, names[i]);
                    var permission = GetPermission(physicalPath, names[i], IsFile);
                    if (permission != null && (!permission.Read || !permission.Write))
                    {
                        accessMessage = permission.Message;
                        throw new UnauthorizedAccessException("'" + getFileNameFromPath(rootName + path + names[i]) + "' is not accessible. You need permission to perform the write action.");
                    }
                }
                var PathPermission = GetPathPermission(targetPath);
                if (PathPermission != null && (!PathPermission.Read || !PathPermission.WriteContents))
                {
                    accessMessage = PathPermission.Message;
                    throw new UnauthorizedAccessException("'" + getFileNameFromPath(rootName + targetPath) + "' is not accessible. You need permission to perform the writeContents action.");
                }

                List<string> existFiles = new();
                List<string> missingFiles = new();
                List<FileManagerDirectoryContent> movedFiles = new();
                var tempPath = path;
                for (var i = 0; i < names.Length; i++)
                {
                    var fullName = names[i];
                    var name = names[i].LastIndexOf("/");
                    if (name >= 0)
                    {
                        path = tempPath + names[i].Substring(0, name + 1);
                        names[i] = names[i].Substring(name + 1);
                    }
                    else
                    {
                        path = tempPath;
                    }
                    var itemPath = Path.Combine(contentRootPath + path, names[i]);
                    itemPath = itemPath.Replace("../", "");
                    if (Directory.Exists(itemPath) || File.Exists(itemPath))
                    {
                        if ((File.GetAttributes(itemPath) & FileAttributes.Directory) == FileAttributes.Directory)
                        {
                            var directoryName = names[i];
                            var oldPath = Path.Combine(contentRootPath + path, directoryName);
                            oldPath = oldPath.Replace("../", "");
                            var newPath = Path.Combine(contentRootPath + targetPath, directoryName);
                            newPath = newPath.Replace("../", "");
                            var exist = Directory.Exists(newPath);
                            if (exist)
                            {
                                var index = -1;
                                if (renameFiles.Length > 0)
                                {
                                    index = Array.FindIndex(renameFiles, row => row.Contains(directoryName));
                                }
                                if ((newPath == oldPath) || (index != -1))
                                {
                                    newPath = DirectoryRename(newPath);
                                    result = DirectoryCopy(oldPath, newPath);
                                    if (result != String.Empty) { break; }
                                    var isExist = Directory.Exists(oldPath);
                                    if (isExist)
                                    {
                                        result = DeleteDirectory(oldPath);
                                        if (result != String.Empty) { break; }
                                    }
                                    var detail = GetFileDetails(newPath);
                                    detail.PreviousName = names[i];
                                    movedFiles.Add(detail);
                                }
                                else
                                {
                                    existFiles.Add(fullName);
                                }
                            }
                            else
                            {
                                result = DirectoryCopy(oldPath, newPath);
                                if (result != String.Empty) { break; }
                                var isExist = Directory.Exists(oldPath);
                                if (isExist)
                                {
                                    result = DeleteDirectory(oldPath);
                                    if (result != String.Empty) { break; }
                                }
                                var detail = GetFileDetails(newPath);
                                detail.PreviousName = names[i];
                                movedFiles.Add(detail);
                            }
                        }
                        else
                        {
                            var fileName = names[i];
                            var oldPath = Path.Combine(contentRootPath + path, fileName);
                            oldPath = oldPath.Replace("../", "");
                            var newPath = Path.Combine(contentRootPath + targetPath, fileName);
                            newPath = newPath.Replace("../", "");
                            var fileExist = File.Exists(newPath);
                            try
                            {

                                if (fileExist)
                                {
                                    var index = -1;
                                    if (renameFiles.Length > 0)
                                    {
                                        index = Array.FindIndex(renameFiles, row => row.Contains(fileName));
                                    }
                                    if ((newPath == oldPath) || (index != -1))
                                    {
                                        newPath = FileRename(newPath, fileName);
                                        File.Copy(oldPath, newPath);
                                        var isExist = File.Exists(oldPath);
                                        if (isExist)
                                        {
                                            File.Delete(oldPath);
                                        }
                                        var detail = GetFileDetails(newPath);
                                        detail.PreviousName = names[i];
                                        movedFiles.Add(detail);
                                    }
                                    else
                                    {
                                        existFiles.Add(fullName);
                                    }
                                }
                                else
                                {
                                    File.Copy(oldPath, newPath);
                                    var isExist = File.Exists(oldPath);
                                    if (isExist)
                                    {
                                        File.Delete(oldPath);
                                    }
                                    var detail = GetFileDetails(newPath);
                                    detail.PreviousName = names[i];
                                    movedFiles.Add(detail);
                                }

                            }
                            catch (Exception e)
                            {
                                if (e.GetType().Name == "UnauthorizedAccessException")
                                {
                                    result = newPath;
                                    break;
                                }
                                else
                                {
                                    throw e;
                                }
                            }
                        }
                    }
                    else
                    {
                        missingFiles.Add(names[i]);
                    }
                }
                moveResponse.Files = movedFiles;
                if (result != String.Empty)
                {
                    var deniedPath = result.Substring(contentRootPath.Length);
                    var er = new ErrorDetails();
                    er.Message = "'" + getFileNameFromPath(deniedPath) + "' is not accessible. You need permission to perform this action.";
                    er.Code = "401";
                    moveResponse.Error = er;
                    return moveResponse;
                }
                if (existFiles.Count > 0)
                {
                    var er = new ErrorDetails();
                    er.FileExists = existFiles;
                    er.Code = "400";
                    er.Message = "File Already Exists";
                    moveResponse.Error = er;
                }
                if (missingFiles.Count == 0)
                {
                    return moveResponse;
                }
                else
                {
                    var namelist = missingFiles[0];
                    for (var k = 1; k < missingFiles.Count; k++)
                    {
                        namelist = namelist + ", " + missingFiles[k];
                    }
                    throw new FileNotFoundException(namelist + " not found in given location.");
                }
            }
            catch (Exception e)
            {
                var er = new ErrorDetails
                {
                    Message = e.Message.ToString(),
                    Code = e.Message.ToString().Contains("is not accessible. You need permission") ? "401" : "417",
                    FileExists = moveResponse.Error?.FileExists
                };
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                moveResponse.Error = er;
                return moveResponse;
            }
        }

        public FileManagerResponse Search(string path, string searchString, bool showHiddenItems = false, bool caseSensitive = false, params FileManagerDirectoryContent[] data)
        {
            var searchResponse = new FileManagerResponse();
            try
            {
                if (path == null) { path = string.Empty; };
                var searchWord = searchString;
                var searchPath = (contentRootPath + path);
                var directory = new DirectoryInfo(contentRootPath + path);
                var cwd = new FileManagerDirectoryContent();
                cwd.Name = directory.Name;
                cwd.Size = 0;
                cwd.IsFile = false;
                cwd.DateModified = directory.LastWriteTime;
                cwd.DateCreated = directory.CreationTime;
                var rootPath = string.IsNullOrEmpty(hostPath) ? contentRootPath : new DirectoryInfo(hostPath).FullName;
                var parentPath = string.IsNullOrEmpty(hostPath) ? directory.Parent.FullName : new DirectoryInfo(hostPath + (path != "/" ? path : "")).Parent.FullName;
                cwd.HasChild = CheckChild(directory.FullName);
                cwd.Type = directory.Extension;
                cwd.FilterPath = GetRelativePath(rootPath, parentPath + Path.DirectorySeparatorChar);
                cwd.Permission = GetPathPermission(path);
                if (cwd.Permission != null && !cwd.Permission.Read)
                {
                    accessMessage = cwd.Permission.Message;
                    throw new UnauthorizedAccessException("'" + getFileNameFromPath(rootName + path) + "' is not accessible. You need permission to perform the read action.");
                }
                searchResponse.CWD = cwd;

                List<FileManagerDirectoryContent> foundedFiles = new();
                var extensions = allowedExtension;
                var searchDirectory = new DirectoryInfo(searchPath);
                List<FileInfo> files = new();
                List<DirectoryInfo> directories = new();
                if (showHiddenItems)
                {
                    IEnumerable<FileInfo> filteredFileList = GetDirectoryFiles(searchDirectory, files).
                        Where(item => new Regex(WildcardToRegex(searchString), (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)).IsMatch(item.Name));
                    IEnumerable<DirectoryInfo> filteredDirectoryList = GetDirectoryFolders(searchDirectory, directories).
                        Where(item => new Regex(WildcardToRegex(searchString), (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)).IsMatch(item.Name));
                    foreach (var file in filteredFileList)
                    {
                        var fileDetails = GetFileDetails(Path.Combine(contentRootPath, file.DirectoryName, file.Name));
                        var hasPermission = parentsHavePermission(fileDetails);
                        if (hasPermission)
                        {
                            foundedFiles.Add(fileDetails);
                        }
                    }
                    foreach (var dir in filteredDirectoryList)
                    {
                        var dirDetails = GetFileDetails(Path.Combine(contentRootPath, dir.FullName));
                        var hasPermission = parentsHavePermission(dirDetails);
                        if (hasPermission)
                        {
                            foundedFiles.Add(dirDetails);
                        }
                    }
                }
                else
                {
                    IEnumerable<FileInfo> filteredFileList = GetDirectoryFiles(searchDirectory, files).
                       Where(item => new Regex(WildcardToRegex(searchString), (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)).IsMatch(item.Name) && (item.Attributes & FileAttributes.Hidden) == 0);
                    IEnumerable<DirectoryInfo> filteredDirectoryList = GetDirectoryFolders(searchDirectory, directories).
                        Where(item => new Regex(WildcardToRegex(searchString), (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)).IsMatch(item.Name) && (item.Attributes & FileAttributes.Hidden) == 0);
                    foreach (var file in filteredFileList)
                    {
                        var fileDetails = GetFileDetails(Path.Combine(contentRootPath, file.DirectoryName, file.Name));
                        var hasPermission = parentsHavePermission(fileDetails);
                        if (hasPermission)
                        {
                            foundedFiles.Add(fileDetails);
                        }
                    }
                    foreach (var dir in filteredDirectoryList)
                    {
                        var dirDetails = GetFileDetails(Path.Combine(contentRootPath, dir.FullName));
                        var hasPermission = parentsHavePermission(dirDetails);
                        if (hasPermission)
                        {
                            foundedFiles.Add(dirDetails);
                        }
                    }
                }
                searchResponse.Files = (IEnumerable<FileManagerDirectoryContent>)foundedFiles;
                return searchResponse;
            }
            catch (Exception e)
            {
                var er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                searchResponse.Error = er;
                return searchResponse;
            }
        }

        private String byteConversion(long fileSize)
        {
            try
            {
                string[] index = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
                if (fileSize == 0)
                {
                    return "0 " + index[0];
                }

                var bytes = Math.Abs(fileSize);
                var loc = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
                var num = Math.Round(bytes / Math.Pow(1024, loc), 1);
                return (Math.Sign(fileSize) * num).ToString() + " " + index[loc];
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern)
                              .Replace(@"\*", ".*")
                              .Replace(@"\?", ".")
                       + "$";
        }

        public FileStreamResult GetImage(string path, string id, bool allowCompress, ImageSize? size, params FileManagerDirectoryContent[]? data)
        {
            try
            {
                var PathPermission = GetFilePermission(path);
                if (PathPermission != null && !PathPermission.Read)
                    return null;
                var fullPath = (contentRootPath + path);
                fullPath = fullPath.Replace("../", "");
#if EJ2_DNX
                if (allowCompress)
                {
                    size = new ImageSize { Height = 14, Width = 16 };
                    CompressImage(fullPath, size);
                }
#endif

                var fileStreamInput = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                var fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                return fileStreamResult;
            }
            catch (Exception)
            {
                return null;
            }
        }

#if EJ2_DNX
        protected virtual void CompressImage(string path, ImageSize targetSize)
        {
            using (var image = Image.FromStream(System.IO.File.OpenRead(path)))
            {
                var originalSize = new ImageSize { Height = image.Height, Width = image.Width };
                var size = FindRatio(originalSize, targetSize);
                using (var thumbnail = new Bitmap(size.Width, size.Height))
                {
                    using (var graphics = Graphics.FromImage(thumbnail))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        graphics.PixelOffsetMode = PixelOffsetMode.Default;
                        graphics.InterpolationMode = InterpolationMode.Bicubic;
                        graphics.DrawImage(image, 0, 0, thumbnail.Width, thumbnail.Height);
                    }

                    using (var memoryStream = new MemoryStream())
                    {
                        thumbnail.Save(memoryStream, ImageFormat.Png);
                        HttpResponse response = HttpContext.Current.Response;
                        response.Buffer = true;
                        response.Clear();
                        response.ContentType = "image/png";
                        response.BinaryWrite(memoryStream.ToArray());
                        response.Flush();
                        response.End();
                    }
                }
            }
        }
      
        protected virtual ImageSize FindRatio(ImageSize originalSize, ImageSize targetSize)
        {
            var aspectRatio = (float)originalSize.Width / (float)originalSize.Height;
            var width = targetSize.Width;
            var height = targetSize.Height;

            if (originalSize.Width > targetSize.Width || originalSize.Height > targetSize.Height)
            {
                if (aspectRatio > 1)
                {
                    height = (int)(targetSize.Height / aspectRatio);
                }
                else
                {
                    width = (int)(targetSize.Width * aspectRatio);
                }
            }
            else
            {
                width = originalSize.Width;
                height = originalSize.Height;
            }

            return new ImageSize
            {
                Width = Math.Max(width, 1),
                Height = Math.Max(height, 1)
            };
        }
#endif
#if EJ2_DNX
        public virtual FileManagerResponse Upload(string path, IList<System.Web.HttpPostedFileBase> uploadFiles, string action, params FileManagerDirectoryContent[] data)
#else
        public FileManagerResponse Upload(string path, IList<IFormFile> uploadFiles, string action, params FileManagerDirectoryContent[]? data)
#endif
        {
            var uploadResponse = new FileManagerResponse();
            try
            {
                var PathPermission = GetPathPermission(path);
                if (PathPermission != null && (!PathPermission.Read || !PathPermission.Upload))
                {
                    accessMessage = PathPermission.Message;
                    throw new UnauthorizedAccessException("'" + getFileNameFromPath(rootName + path) + "' is not accessible. You need permission to perform the upload action.");
                }

                List<string> existFiles = new();
#if EJ2_DNX
                foreach (System.Web.HttpPostedFileBase file in uploadFiles)
#else
                foreach (var file in uploadFiles)
#endif
                {
                    if (uploadFiles != null)
                    {
#if EJ2_DNX
                        var name = System.IO.Path.GetFileName(file.FileName);
                        var fullName = Path.Combine((this.contentRootPath + path), name);
#else
                        var name = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim().ToString();
                        var folders = name.Split('/');
                        var fileName = folders[folders.Length - 1];
                        var fullName = Path.Combine((contentRootPath + path), fileName);
                        fullName = fullName.Replace("../", "");
#endif
                        if (action == "save")
                        {
                            if (!File.Exists(fullName))
                            {
#if !EJ2_DNX
                                using (var fs = File.Create(fullName))
                                {
                                    file.CopyTo(fs);
                                    fs.Flush();
                                }
#else
                                file.SaveAs(fullName);
#endif
                            }
                            else
                            {
                                existFiles.Add(fullName);
                            }
                        }
                        else if (action == "remove")
                        {
                            if (File.Exists(fullName))
                            {
                                File.Delete(fullName);
                            }
                            else
                            {
                                var er = new ErrorDetails();
                                er.Code = "404";
                                er.Message = "File not found.";
                                uploadResponse.Error = er;
                            }
                        }
                        else if (action == "replace")
                        {
                            if (File.Exists(fullName))
                            {
                                File.Delete(fullName);
                            }
#if !EJ2_DNX
                            using (var fs = File.Create(fullName))
                            {
                                file.CopyTo(fs);
                                fs.Flush();
                            }
#else
                            file.SaveAs(fullName);
#endif
                        }
                        else if (action == "keepboth")
                        {
                            var newName = fullName;
                            var index = newName.LastIndexOf(".");
                            if (index >= 0)
                                newName = newName.Substring(0, index);
                            var fileCount = 0;
                            while (File.Exists(newName + (fileCount > 0 ? "(" + fileCount.ToString() + ")" + Path.GetExtension(name) : Path.GetExtension(name))))
                            {
                                fileCount++;
                            }
                            newName = newName + (fileCount > 0 ? "(" + fileCount.ToString() + ")" : "") + Path.GetExtension(name);
#if !EJ2_DNX
                            using (var fs = File.Create(newName))
                            {
                                file.CopyTo(fs);
                                fs.Flush();
                            }
#else
                            file.SaveAs(newName);
#endif
                        }
                    }
                }
                if (existFiles.Count != 0)
                {
                    var er = new ErrorDetails();
                    er.Code = "400";
                    er.Message = "File already exists.";
                    er.FileExists = existFiles;
                    uploadResponse.Error = er;
                }
                return uploadResponse;
            }
            catch (Exception e)
            {
                var er = new ErrorDetails();

                er.Message = (e.GetType().Name == "UnauthorizedAccessException") ? "'" + getFileNameFromPath(path) + "' is not accessible. You need permission to perform the upload action." : e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                uploadResponse.Error = er;
                return uploadResponse;
            }
        }
#if SyncfusionFramework4_0
        public virtual void Download(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            try
            {
                string physicalPath = GetPath(path);
                String extension;
                int count = 0;
                for (var i = 0; i < names.Length; i++)
                {
                    bool IsFile = !IsDirectory(physicalPath, names[i]);
                    AccessPermission FilePermission = GetPermission(physicalPath, names[i], IsFile);
                    if (FilePermission != null && (!FilePermission.Read || !FilePermission.Download))
                     throw new UnauthorizedAccessException("'" + this.getFileNameFromPath(this.rootName + path + names[i]) + "' is not accessible. You need permission to perform the download action.");

                    extension = Path.GetExtension(names[i]);
                    if (extension != "")
                    {
                        count++;
                    }
                }
                if (names.Length > 1)
                    DownloadZip(path, names);

                if (count == names.Length)
                {
                    DownloadFile(path, names);
                }

            }
            catch (Exception)
            {

            }
        }

        private FileStreamResult fileStreamResult;
        protected virtual void DownloadFile(string path, string[] names = null)
        {

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    path = (Path.Combine(contentRootPath + path, names[0]));
                    HttpResponse response = HttpContext.Current.Response;
                    response.Buffer = true;
                    response.Clear();
                    response.ContentType = "APPLICATION/octet-stream";
                    string extension = System.IO.Path.GetExtension(path);
                    response.AddHeader("content-disposition", string.Format("attachment; filename = \"{0}\"", System.IO.Path.GetFileName(path)));
                    response.WriteFile(path);
                    response.Flush();
                    response.End();
                }
                catch (Exception ex) { throw ex; }
            }
            else throw new ArgumentNullException("name should not be null");

        }

        protected virtual void DownloadZip(string path, string[] names)
        {
            HttpResponse response = HttpContext.Current.Response;
            string tempPath = Path.Combine(Path.GetTempPath(), "temp.zip");

            for (int i = 0; i < names.Count(); i++)
            {
                string fullPath = Path.Combine(contentRootPath + path, names[0]);
                if (!string.IsNullOrEmpty(fullPath))
                {
                    try
                    {
                        var physicalPath = Path.Combine(contentRootPath + path, names[0]);
                        AddFileToZip(tempPath, physicalPath);
                    }
                    catch (Exception ex) { throw ex; }
                }
                else throw new ArgumentNullException("name should not be null");
            }
            try
            {
                System.Net.WebClient net = new System.Net.WebClient();
                response.ClearHeaders();
                response.Clear();
                response.Expires = 0;
                response.Buffer = true;
                response.AddHeader("Content-Disposition", "Attachment;FileName=Files.zip");
                response.ContentType = "application/zip";
                response.BinaryWrite(net.DownloadData(tempPath));
                response.End();
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
            catch (Exception ex) { throw ex; }
        }

        protected virtual void AddFileToZip(string zipFileName, string fileToAdd)
        {
            using (Package zip = System.IO.Packaging.Package.Open(zipFileName, FileMode.OpenOrCreate))
            {
                string destFilename = ".\\" + Path.GetFileName(fileToAdd);
                Uri uri = PackUriHelper.CreatePartUri(new Uri(destFilename, UriKind.Relative));
                if (zip.PartExists(uri))
                    zip.DeletePart(uri);
                PackagePart pkgPart = zip.CreatePart(uri, System.Net.Mime.MediaTypeNames.Application.Zip, CompressionOption.Normal);
                Byte[] bites = System.IO.File.ReadAllBytes(fileToAdd);
                pkgPart.GetStream().Write(bites, 0, bites.Length);
                zip.Close();
            }
        }

#else

        public FileStreamResult Download(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            try
            {
                var physicalPath = GetPath(path);
                String fullPath;
                var count = 0;
                for (var i = 0; i < names.Length; i++)
                {
                    var IsFile = !IsDirectory(physicalPath, names[i]);
                    var FilePermission = GetPermission(physicalPath, names[i], IsFile);
                    if (FilePermission != null && (!FilePermission.Read || !FilePermission.Download))
                        throw new UnauthorizedAccessException("'" + rootName + path + names[i] + "' is not accessible. Access is denied.");

                    fullPath = Path.Combine(contentRootPath + path, names[i]);
                    fullPath = fullPath.Replace("../", "");
                    if ((File.GetAttributes(fullPath) & FileAttributes.Directory) != FileAttributes.Directory)
                    {
                        count++;
                    }
                }
                if (count == names.Length)
                {
                    return DownloadFile(path, names);
                }
                else
                {
                    return DownloadFolder(path, names, count);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private FileStreamResult fileStreamResult;

        private FileStreamResult DownloadFile(string? path, string[] names)
        {
            try
            {
                path = Path.GetDirectoryName(path);
                var tempPath = Path.Combine(Path.GetTempPath(), "temp.zip");
                string fullPath;
                if (names.Length == 0)
                {
                    fullPath = (contentRootPath + path);
                    fullPath = fullPath.Replace("../", "");
                    var bytes = File.ReadAllBytes(fullPath);
                    var fileStreamInput = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                    fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                }
                else switch (names.Length)
                {
                    case 1:
                    {
                        fullPath = Path.Combine(contentRootPath + path, names[0]);
                        fullPath = fullPath.Replace("../", "");
                        var fileStreamInput = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                        fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream")
                        {
                            FileDownloadName = names[0]
                        };
                        break;
                    }
                    case > 1:
                    {
                        var fileName = Guid.NewGuid().ToString() + "temp.zip";
                        var newFileName = fileName.Substring(36);
                        tempPath = Path.Combine(Path.GetTempPath(), newFileName);
                        tempPath = tempPath.Replace("../", "");
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                        string currentDirectory;
                        ZipArchiveEntry zipEntry;
                        ZipArchive archive;
                        for (var i = 0; i < names.Count(); i++)
                        {
                            fullPath = Path.Combine((contentRootPath + path), names[i]);
                            fullPath = fullPath.Replace("../", "");
                            if (!string.IsNullOrEmpty(fullPath))
                            {
                                try
                                {
                                    using (archive = ZipFile.Open(tempPath, ZipArchiveMode.Update))
                                    {
                                        currentDirectory = Path.Combine((contentRootPath + path), names[i]);
                                        currentDirectory = currentDirectory.Replace("../", "");

#if SyncfusionFramework4_5
                                    zipEntry = archive.CreateEntryFromFile(Path.Combine(this.contentRootPath, currentDirectory), names[i]);
#else
                                        zipEntry = archive.CreateEntryFromFile(Path.Combine(contentRootPath, currentDirectory), names[i], CompressionLevel.Fastest);
#endif
                                    }
                                }
                                catch (Exception)
                                {
                                    return null;
                                }
                            }
                            else
                            {
                                throw new ArgumentNullException("name should not be null");
                            }
                        }
                        try
                        {
                            var fileStreamInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                            fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                            fileStreamResult.FileDownloadName = "files.zip";
                        }
                        catch (Exception)
                        {
                            return null;
                        }

                        break;
                    }
                }
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                return fileStreamResult;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private FileStreamResult DownloadFolder(string path, string[] names, int count)
        {
            try
            {
                if (!String.IsNullOrEmpty(path))
                {
                    path = Path.GetDirectoryName(path);
                }
                FileStreamResult fileStreamResult;
                // create a temp.Zip file intially 
                var tempPath = Path.Combine(Path.GetTempPath(), "temp.zip");
                String fullPath;
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                if (names.Length == 1)
                {
                    fullPath = Path.Combine(contentRootPath + path, names[0]);
                    fullPath = fullPath.Replace("../", "");
                    var directoryName = new DirectoryInfo(fullPath);

#if SyncfusionFramework4_5
                    ZipFile.CreateFromDirectory(fullPath, tempPath);
#else
                    ZipFile.CreateFromDirectory(fullPath, tempPath, CompressionLevel.Fastest, true);
#endif
                    var fileStreamInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                    fileStreamResult = new FileStreamResult(fileStreamInput, "APPLICATION/octet-stream");
                    fileStreamResult.FileDownloadName = directoryName.Name + ".zip";
                }
                else
                {
                    string currentDirectory;
                    ZipArchiveEntry zipEntry;
                    ZipArchive archive;
                    using (archive = ZipFile.Open(tempPath, ZipArchiveMode.Update))
                    {
                        for (var i = 0; i < names.Length; i++)
                        {
                            currentDirectory = Path.Combine((contentRootPath + path), names[i]);
                            currentDirectory = currentDirectory.Replace("../", "");
                            if ((File.GetAttributes(currentDirectory) & FileAttributes.Directory) == FileAttributes.Directory)
                            {
                                var files = Directory.GetFiles(currentDirectory, "*.*", SearchOption.AllDirectories);
                                if (files.Length == 0)
                                {
                                    zipEntry = archive.CreateEntry(names[i] + "/");
                                }
                                else
                                {
                                    foreach (var filePath in files)
                                    {
#if SyncfusionFramework4_5
                                    zipEntry = archive.CreateEntryFromFile(filePath, names[i] + filePath.Substring(currentDirectory.Length));
#else
                                        zipEntry = archive.CreateEntryFromFile(filePath, names[i] + filePath.Substring(currentDirectory.Length), CompressionLevel.Fastest);
#endif

                                    }
                                }
                                foreach (var filePath in Directory.GetDirectories(currentDirectory, "*", SearchOption.AllDirectories))
                                {
                                    if (Directory.GetFiles(filePath).Length == 0)
                                    {
#if SyncfusionFramework4_5
                                            zipEntry = archive.CreateEntryFromFile(Path.Combine(this.contentRootPath, filePath), filePath.Substring(path.Length));
#else
                                        zipEntry = archive.CreateEntry(names[i] + filePath.Substring(currentDirectory.Length) + "/");
#endif
                                    }
                                }
                            }
                            else
                            {
#if SyncfusionFramework4_5
                                    zipEntry = archive.CreateEntryFromFile(Path.Combine(this.contentRootPath, currentDirectory), names[i]);
#else
                                zipEntry = archive.CreateEntryFromFile(Path.Combine(contentRootPath, currentDirectory), names[i], CompressionLevel.Fastest);
#endif

                            }
                        }
                    }
                    var fileStreamInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                    fileStreamResult = new FileStreamResult(fileStreamInput, "application/force-download");
                    fileStreamResult.FileDownloadName = "folders.zip";
                }
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                return fileStreamResult;
            }
            catch (Exception)
            {
                return null;
            }
        }

#endif
        private string DirectoryRename(string newPath)
        {
            var directoryCount = 0;
            while (Directory.Exists(newPath + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "")))
            {
                directoryCount++;
            }
            newPath = newPath + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "");
            return newPath;
        }

        private string FileRename(string newPath, string fileName)
        {
            var name = newPath.LastIndexOf(".");
            if (name >= 0)
            {
                newPath = newPath.Substring(0, name);
            }
            var fileCount = 0;
            while (File.Exists(newPath + (fileCount > 0 ? "(" + fileCount.ToString() + ")" + Path.GetExtension(fileName) : Path.GetExtension(fileName))))
            {
                fileCount++;
            }
            newPath = newPath + (fileCount > 0 ? "(" + fileCount.ToString() + ")" : "") + Path.GetExtension(fileName);
            return newPath;
        }


        private string DirectoryCopy(string sourceDirName, string destDirName)
        {
            var result = String.Empty;
            try
            {
                // Gets the subdirectories for the specified directory.
                var dir = new DirectoryInfo(sourceDirName);

                var dirs = dir.GetDirectories();
                // If the destination directory doesn't exist, creates it.
                if (!Directory.Exists(destDirName))
                {
                    try
                    {
                        Directory.CreateDirectory(destDirName);
                    }
                    catch (Exception e)
                    {
                        if (e.GetType().Name == "UnauthorizedAccessException")
                        {
                            return destDirName;
                        }
                        else
                        {
                            throw e;
                        }
                    }
                }

                // Gets the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                foreach (var file in files)
                {
                    try
                    {
                        var oldPath = Path.Combine(sourceDirName, file.Name);
                        oldPath = oldPath.Replace("../", "");
                        var temppath = Path.Combine(destDirName, file.Name);
                        temppath = temppath.Replace("../", "");
                        File.Copy(oldPath, temppath);
                    }
                    catch (Exception e)
                    {
                        if (e.GetType().Name == "UnauthorizedAccessException")
                        {
                            return file.FullName;
                        }
                        else
                        {
                            throw e;
                        }
                    }
                }
                foreach (var direc in dirs)
                {
                    var oldPath = Path.Combine(sourceDirName, direc.Name);
                    oldPath = oldPath.Replace("../", "");
                    var temppath = Path.Combine(destDirName, direc.Name);
                    temppath = temppath.Replace("../", "");
                    result = DirectoryCopy(oldPath, temppath);
                    if (result != String.Empty)
                    {
                        return result;
                    }
                }
                return result;
            }
            catch (Exception e)
            {
                if (e.GetType().Name == "UnauthorizedAccessException")
                {
                    return sourceDirName;
                }
                else
                {
                    throw e;
                }
            }
        }


        private string DeleteDirectory(string path)
        {
            try
            {
                var result = String.Empty;
                var files = Directory.GetFiles(path);
                string[] dirs = Directory.GetDirectories(path);
                foreach (var file in files)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch (Exception e)
                    {
                        if (e.GetType().Name == "UnauthorizedAccessException")
                        {
                            return file;
                        }
                        else
                        {
                            throw e;
                        }
                    }
                }
                foreach (var dir in dirs)
                {
                    result = DeleteDirectory(dir);
                    if (result != String.Empty)
                    {
                        return result;
                    }
                }
                Directory.Delete(path, true);
                return result;
            }
            catch (Exception e)
            {
                if (e.GetType().Name == "UnauthorizedAccessException")
                {
                    return path;
                }
                else
                {
                    throw e;
                }

            }
        }

        private FileManagerDirectoryContent GetFileDetails(string path)
        {
            try
            {
                var info = new FileInfo(path);
                var attr = File.GetAttributes(path);
                var detailPath = new FileInfo(info.FullName);
                var folderLength = 0;
                var isFile = ((attr & FileAttributes.Directory) == FileAttributes.Directory) ? false : true;
                if (!isFile)
                {
                    folderLength = detailPath.Directory.GetDirectories().Length;
                }
                var filterPath = GetRelativePath(contentRootPath, info.DirectoryName + Path.DirectorySeparatorChar);
                return new FileManagerDirectoryContent
                {
                    Name = info.Name,
                    Size = isFile ? info.Length : 0,
                    IsFile = isFile,
                    DateModified = info.LastWriteTime,
                    DateCreated = info.CreationTime,
                    Type = info.Extension,
                    HasChild = isFile ? false : (CheckChild(info.FullName)),
                    FilterPath = filterPath,
                    Permission = GetPermission(GetPath(filterPath), info.Name, isFile)
                };
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private AccessPermission GetPermission(string location, string name, bool isFile)
        {
            var FilePermission = new AccessPermission();
            if (isFile)
            {
                if (AccessDetails.AccessRules == null) return null;
                var nameExtension = Path.GetExtension(name).ToLower();
                var fileName = Path.GetFileNameWithoutExtension(name);
                var currentPath = GetFilePath(location + name);
                foreach (var fileRule in AccessDetails.AccessRules)
                {
                    if (!string.IsNullOrEmpty(fileRule.Path) && fileRule.IsFile && (fileRule.Role == null || fileRule.Role == AccessDetails.Role))
                    {
                        if (fileRule.Path.IndexOf("*.*") > -1)
                        {
                            var parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf("*.*"));
                            if (currentPath.IndexOf(GetPath(parentPath)) == 0 || parentPath == "")
                            {
                                FilePermission = UpdateFileRules(FilePermission, fileRule);
                            }
                        }
                        else if (fileRule.Path.IndexOf("*.") > -1)
                        {
                            var pathExtension = Path.GetExtension(fileRule.Path).ToLower();
                            var parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf("*."));
                            if ((GetPath(parentPath) == currentPath || parentPath == "") && nameExtension == pathExtension)
                            {
                                FilePermission = UpdateFileRules(FilePermission, fileRule);
                            }
                        }
                        else if (fileRule.Path.IndexOf(".*") > -1)
                        {
                            var pathName = Path.GetFileNameWithoutExtension(fileRule.Path);
                            var parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf(pathName + ".*"));
                            if ((GetPath(parentPath) == currentPath || parentPath == "") && fileName == pathName)
                            {
                                FilePermission = UpdateFileRules(FilePermission, fileRule);
                            }
                        }
                        else if (GetPath(fileRule.Path) == GetValidPath(location + name))
                        {
                            FilePermission = UpdateFileRules(FilePermission, fileRule);
                        }
                    }
                }
                return FilePermission;
            }
            else
            {
                if (AccessDetails.AccessRules == null) { return null; }
                foreach (var folderRule in AccessDetails.AccessRules)
                {
                    if (folderRule.Path != null && folderRule.IsFile == false && (folderRule.Role == null || folderRule.Role == AccessDetails.Role))
                    {
                        if (folderRule.Path.IndexOf("*") > -1)
                        {
                            var parentPath = folderRule.Path.Substring(0, folderRule.Path.IndexOf("*"));
                            if (GetValidPath(location + name).IndexOf(GetPath(parentPath)) == 0 || parentPath == "")
                            {
                                FilePermission = UpdateFolderRules(FilePermission, folderRule);
                            }
                        }
                        else if (GetPath(folderRule.Path) == GetValidPath(location + name) || GetPath(folderRule.Path) == GetValidPath(location + name + Path.DirectorySeparatorChar))
                        {
                            FilePermission = UpdateFolderRules(FilePermission, folderRule);
                        }
                        else if (GetValidPath(location + name).IndexOf(GetPath(folderRule.Path)) == 0)
                        {
                            FilePermission.Write = HasPermission(folderRule.WriteContents);
                            FilePermission.WriteContents = HasPermission(folderRule.WriteContents);
                        }
                    }
                }
                return FilePermission;
            }
        }

        private string GetPath(string path)
        {
            var fullPath = (contentRootPath + path);
            fullPath = fullPath.Replace("../", "");
            var directory = new DirectoryInfo(fullPath);
            return directory.FullName;
        }

        private string GetValidPath(string path)
        {
            var directory = new DirectoryInfo(path);
            return directory.FullName;
        }

        private string GetFilePath(string path)
        {
            return Path.GetDirectoryName(path) + Path.DirectorySeparatorChar;
        }

        private string[] GetFolderDetails(string path)
        {
            string[] str_array = path.Split('/'), fileDetails = new string[2];
            var parentPath = "";
            for (var i = 0; i < str_array.Length - 2; i++)
            {
                parentPath += str_array[i] + "/";
            }
            fileDetails[0] = parentPath;
            fileDetails[1] = str_array[str_array.Length - 2];
            return fileDetails;
        }

        private AccessPermission GetPathPermission(string path)
        {
            var fileDetails = GetFolderDetails(path);
            return GetPermission(GetPath(fileDetails[0]), fileDetails[1], false);
        }

        private AccessPermission GetFilePermission(string path)
        {
            var parentPath = path.Substring(0, path.LastIndexOf("/") + 1);
            var fileName = Path.GetFileName(path);
            return GetPermission(GetPath(parentPath), fileName, true);
        }

        private bool IsDirectory(string path, string fileName)
        {
            var fullPath = Path.Combine(path, fileName);
            fullPath = fullPath.Replace("../", "");
            return ((File.GetAttributes(fullPath) & FileAttributes.Directory) != FileAttributes.Directory) ? false : true;
        }

        private bool HasPermission(Permission rule)
        {
            return rule == Permission.Allow ? true : false;
        }

        private AccessPermission UpdateFileRules(AccessPermission filePermission, AccessRule fileRule)
        {
            filePermission.Copy = HasPermission(fileRule.Copy);
            filePermission.Download = HasPermission(fileRule.Download);
            filePermission.Write = HasPermission(fileRule.Write);
            filePermission.Read = HasPermission(fileRule.Read);
            filePermission.Message = string.IsNullOrEmpty(fileRule.Message) ? string.Empty : fileRule.Message;
            return filePermission;
        }

        private AccessPermission UpdateFolderRules(AccessPermission folderPermission, AccessRule folderRule)
        {
            folderPermission.Copy = HasPermission(folderRule.Copy);
            folderPermission.Download = HasPermission(folderRule.Download);
            folderPermission.Write = HasPermission(folderRule.Write);
            folderPermission.WriteContents = HasPermission(folderRule.WriteContents);
            folderPermission.Read = HasPermission(folderRule.Read);
            folderPermission.Upload = HasPermission(folderRule.Upload);
            folderPermission.Message = string.IsNullOrEmpty(folderRule.Message) ? string.Empty : folderRule.Message;
            return folderPermission;
        }

        private bool parentsHavePermission(FileManagerDirectoryContent fileDetails)
        {
            var parentPath = fileDetails.FilterPath.Replace(Path.DirectorySeparatorChar, '/');
            var parents = parentPath.Split('/');
            var currPath = "/";
            var hasPermission = true;
            for (var i = 0; i <= parents.Length - 2; i++)
            {
                currPath = (parents[i] == "") ? currPath : (currPath + parents[i] + "/");
                var PathPermission = GetPathPermission(currPath);
                if (PathPermission == null)
                {
                    break;
                }
                else if (PathPermission != null && !PathPermission.Read)
                {
                    hasPermission = false;
                    break;
                }
            }
            return hasPermission;
        }
        public string ToCamelCase(FileManagerResponse userData)
        {
            return JsonConvert.SerializeObject(userData, new JsonSerializerSettings
            {
#if EJ2_DNX
                ContractResolver = new CamelCasePropertyNamesContractResolver()

#else
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
#endif
            });
        }

        FileStreamResult FileProviderBase.Download(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            throw new NotImplementedException();
        }

        private bool CheckChild(string path)
        {
            bool hasChild;
            try
            {
                var directory = new DirectoryInfo(path);
                var dir = directory.GetDirectories();
                hasChild = dir.Length != 0;
            }
            catch (Exception e)
            {
                if (e.GetType().Name == "UnauthorizedAccessException")
                {
                    hasChild = false;
                }
                else
                {
                    throw e;
                }
            }
            return hasChild;
        }
        private bool hasAccess(string path)
        {
            bool hasAcceess;
            try
            {
                var directory = new DirectoryInfo(path);
                var dir = directory.GetDirectories();
                hasAcceess = dir != null;
            }
            catch (Exception e)
            {
                if (e.GetType().Name == "UnauthorizedAccessException")
                {
                    hasAcceess = false;
                }
                else
                {
                    throw e;
                }
            }
            return hasAcceess;
        }
        private long GetDirectorySize(DirectoryInfo dir, long size)
        {
            try
            {
                foreach (var subdir in dir.GetDirectories())
                {
                    size = GetDirectorySize(subdir, size);
                }
                foreach (var file in dir.GetFiles())
                {
                    size += file.Length;
                }
            }
            catch (Exception e)
            {
                if (e.GetType().Name != "UnauthorizedAccessException")
                {
                    throw e;
                }
            }
            return size;
        }
        private List<FileInfo> GetDirectoryFiles(DirectoryInfo dir, List<FileInfo> files)
        {
            try
            {
                foreach (var subdir in dir.GetDirectories())
                {
                    files = GetDirectoryFiles(subdir, files);
                }
                foreach (var file in dir.GetFiles())
                {
                    files.Add(file);
                }
            }
            catch (Exception e)
            {
                if (e.GetType().Name != "UnauthorizedAccessException")
                {
                    throw e;
                }
            }
            return files;
        }
        private List<DirectoryInfo> GetDirectoryFolders(DirectoryInfo dir, List<DirectoryInfo> files)
        {
            try
            {
                foreach (var subdir in dir.GetDirectories())
                {
                    files = GetDirectoryFolders(subdir, files);
                }
                foreach (var file in dir.GetDirectories())
                {
                    files.Add(file);
                }
            }
            catch (Exception e)
            {
                if (e.GetType().Name != "UnauthorizedAccessException")
                {
                    throw e;
                }
            }
            return files;
        }
        private string getFileNameFromPath(string path)
        {
            var index = path.LastIndexOf("/");
            return path.Substring(index + 1);
        }

    }
}
