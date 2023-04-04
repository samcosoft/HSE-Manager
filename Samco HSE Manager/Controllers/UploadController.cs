using Microsoft.AspNetCore.Mvc;

namespace Samco_HSE_Manager.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UploadController : Controller
{
    private readonly IWebHostEnvironment _hostingEnvironment;

    public UploadController(IWebHostEnvironment hostingEnvironment)
    {
        _hostingEnvironment = hostingEnvironment;
    }


    [HttpPost]
    [Route("UploadSTOPImage")]
    // ReSharper disable once InconsistentNaming
    public ActionResult UploadSTOPImage(IFormFile stopFile)
    {
        try
        {
            // Specify the target location for the uploaded files.
            var path = Path.Combine(_hostingEnvironment.WebRootPath, "upload", "STOPCards");
            // Check whether the target directory exists; otherwise, create it.
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            var orderId = Request.Form["CardId"].ToString();
            path = Path.Combine(path, orderId);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            using var fileStream =
                System.IO.File.Create(Path.Combine(path, SamcoSoftShared.GenerateRandomPass(3,SamcoSoftShared.PassCharacterSwitch.LowerText) + "." + stopFile.FileName.Split(".").Last()));

            // If all checks are passed, save the file.
            stopFile.CopyTo(fileStream);
        }
        catch
        {
            Response.StatusCode = 400;
        }

        return new EmptyResult();
    }
}