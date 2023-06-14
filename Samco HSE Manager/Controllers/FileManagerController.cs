using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Samco_HSE_Manager.Base;

namespace Samco_HSE_Manager.Controllers;

[Route("api/[controller]")]
[EnableCors("AllowAllOrigins")]
public class FileManagerController : Controller
{
    private readonly PhysicalFileProvider _operation;
    private static string _startPath = null!;
    private readonly string _basePath;
    private readonly string _root = "wwwroot\\upload";
    public FileManagerController(IHostEnvironment hostingEnvironment)
    {
        _basePath = hostingEnvironment.ContentRootPath;
        _root = Path.Combine(hostingEnvironment.ContentRootPath, _root);
        _operation = new PhysicalFileProvider();
    }

    [Route("FileOperations")]
    public object? FileOperations([FromBody] FileManagerDirectoryContent args)
    {
        _startPath = args.CustomData["startPath"].ToString()!;
        var path = Path.Combine(_root, _startPath);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        _operation.RootFolder(path);

        if (args.Action is "delete" or "rename")
        {
            if (args.TargetPath == null && (args.Path == ""))
            {
                var response = new FileManagerResponse
                {
                    Error = new ErrorDetails { Code = "401", Message = "Restricted to modify the root folder." }
                };
                return _operation.ToCamelCase(response);
            }
        }

        return args.Action switch
        {
            "read" =>
                // reads the file(s) or folder(s) from the given path.
                _operation.ToCamelCase(_operation.GetFiles(args.Path, args.ShowHiddenItems)),
            "delete" =>
                // deletes the selected file(s) or folder(s) from the given path.
                _operation.ToCamelCase(_operation.Delete(args.Path, args.Names)),
            "copy" =>
                // copies the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                _operation.ToCamelCase(_operation.Copy(args.Path, args.TargetPath!, args.Names, args.RenameFiles,
                    args.TargetData)),
            "move" =>
                // cuts the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                _operation.ToCamelCase(_operation.Move(args.Path, args.TargetPath!, args.Names, args.RenameFiles,
                    args.TargetData)),
            "details" =>
                // gets the details of the selected file(s) or folder(s).
                _operation.ToCamelCase(_operation.Details(args.Path, args.Names, args.Data)),
            "create" =>
                // creates a new folder in a given path.
                _operation.ToCamelCase(_operation.Create(args.Path, args.Name)),
            "search" =>
                // gets the list of file(s) or folder(s) from a given path based on the searched key string.
                _operation.ToCamelCase(_operation.Search(args.Path, args.SearchString, args.ShowHiddenItems,
                    args.CaseSensitive)),
            "rename" =>
                // renames a file or folder.
                _operation.ToCamelCase(_operation.Rename(args.Path, args.Name, args.NewName, false,
                    args.ShowFileExtension, args.Data)),
            _ => null
        };
    }

    // uploads the file(s) into a specified path
    [Route("Upload")]
    public IActionResult Upload(string path, IList<IFormFile> uploadFiles, string action)
    {
        _operation.RootFolder(Path.Combine(_root, _startPath));
        foreach (var file in uploadFiles)
        {
            var folders = (file.FileName).Split('/');
            // checking the folder upload
            if (folders.Length > 1)
            {
                for (var i = 0; i < folders.Length - 1; i++)
                {
                    var newDirectoryPath = Path.Combine(_basePath + path, folders[i]);
                    if (!Directory.Exists(newDirectoryPath))
                    {
                        _operation.ToCamelCase(_operation.Create(path, folders[i]));
                    }
                    path += folders[i] + "/";
                }
            }
        }
        var uploadResponse = _operation.Upload(path, uploadFiles, action, null);
        if (uploadResponse.Error != null)
        {
            Response.Clear();
            Response.ContentType = "application/json; charset=utf-8";
            Response.StatusCode = Convert.ToInt32(uploadResponse.Error.Code);
            Response.HttpContext.Features.Get<IHttpResponseFeature>()!.ReasonPhrase = uploadResponse.Error.Message;
        }
        return Content("");
    }

    // downloads the selected file(s) and folder(s)
    [Route("Download")]
    public IActionResult Download(string downloadInput, string path)
    {
        _operation.RootFolder(Path.Combine(_root, _startPath));
        var args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
        return _operation.Download(args!.Path, args.Names, args.Data);
    }

    // gets the image(s) from the given path
    [Route("GetImage")]
    public IActionResult GetImage(FileManagerDirectoryContent args, string path)
    {
        _operation.RootFolder(Path.Combine(_root, _startPath));
        if (string.IsNullOrEmpty(args.Path)) return Content("");
        return _operation.GetImage(args.Path, args.Id, false, null, null);
    }
}