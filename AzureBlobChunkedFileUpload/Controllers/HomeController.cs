 using AzureBlobChunkedFileUpload.Models;
using Microsoft.Azure;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace AzureBlobChunkedFileUpload.Controllers
    {
        public class HomeController : Controller
        {
            public ActionResult Index()
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
            CloudConfigurationManager.GetSetting("StorageConnectionString"));
                CloudBlobClient storageClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer storageContainer = storageClient.GetContainerReference(
                  ConfigurationManager.AppSettings.Get("CloudStorageContainerReference"));
                CloudFilesModel blobsList = new
                  CloudFilesModel(storageContainer.ListBlobs(useFlatBlobListing: true));
                return View(blobsList);
            }

            public ActionResult UploadFile()
            {
                return View();
            }

            [HttpPost]
            public ActionResult SetMetadata(int blocksCount, string fileName, long fileSize)
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
           CloudConfigurationManager.GetSetting("StorageConnectionString"));
                CloudBlobClient storageClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = storageClient.GetContainerReference(
                  ConfigurationManager.AppSettings.Get("CloudStorageContainerReference"));
                container.CreateIfNotExists();
                var fileToUpload = new CloudFile()
                {
                    BlockCount = blocksCount,
                    FileName = fileName,
                    Size = fileSize,
                    BlockBlob = container.GetBlockBlobReference(fileName),
                    StartTime = DateTime.Now,
                    IsUploadCompleted = false,
                    UploadStatusMessage = string.Empty
                };
                Session.Add("CurrentFile", fileToUpload);
                return Json(true);
            }

            [HttpPost]
            [ValidateInput(false)]
            public ActionResult UploadChunk(int id)
            {
                HttpPostedFileBase request = Request.Files["Slice"];
                byte[] chunk = new byte[request.ContentLength];
                request.InputStream.Read(chunk, 0, Convert.ToInt32(request.ContentLength));
                JsonResult returnData = null;
                string fileSession = "CurrentFile";
                if (Session[fileSession] != null)
                {
                    CloudFile model = (CloudFile)Session[fileSession];
                    returnData = UploadCurrentChunk(model, chunk, id);
                    if (returnData != null)
                    {
                        return returnData;
                    }
                    if (id == model.BlockCount)
                    {
                        return CommitAllChunks(model);
                    }
                }
                else
                {
                    returnData = Json(new
                    {
                        error = true,
                        isLastBlock = false,
                        message = string.Format(CultureInfo.CurrentCulture,
                            "Failed to Upload file.", "Session Timed out")
                    });
                    return returnData;
                }

                return Json(new { error = false, isLastBlock = false, message = string.Empty });
            }

            private ActionResult CommitAllChunks(CloudFile model)
            {
                model.IsUploadCompleted = true;
                bool errorInOperation = false;
                try
                {
                    var blockList = Enumerable.Range(1, (int)model.BlockCount).ToList<int>().ConvertAll(rangeElement =>
                                Convert.ToBase64String(Encoding.UTF8.GetBytes(
                                    string.Format(CultureInfo.InvariantCulture, "{0:D4}", rangeElement))));
                    model.BlockBlob.PutBlockList(blockList);
                    var duration = DateTime.Now - model.StartTime;
                    float fileSizeInKb = model.Size / 1024;
                    string fileSizeMessage = fileSizeInKb > 1024 ?
                        string.Concat(( Math.Round(fileSizeInKb / 1024, 2)).ToString(CultureInfo.CurrentCulture), " MB") :
                        string.Concat(Math.Round(fileSizeInKb,2).ToString(CultureInfo.CurrentCulture), " KB");
                    model.UploadStatusMessage = string.Format(CultureInfo.CurrentCulture,
                        "File uploaded successfully. {0} took {1} seconds to upload",
                        fileSizeMessage, Math.Round(duration.TotalSeconds,2));
                }
                catch (StorageException e)
                {
                    model.UploadStatusMessage = "Failed to Upload file. Exception - " + e.Message;
                    errorInOperation = true;
                }
                finally
                {
                
                Session.Remove("CurrentFile");
                Upload uploadInfo = new Upload();
                uploadInfo.UserName = Session["username"].ToString();
                uploadInfo.CreateTime = model.StartTime;
                uploadInfo.IsUploadCompleted = model.IsUploadCompleted;
                uploadInfo.FileName = model.FileName;

                WriteTableStorage(uploadInfo);



            }
                return Json(new
                {
                    error = errorInOperation,
                    isLastBlock = model.IsUploadCompleted,
                    message = model.UploadStatusMessage
                });
            }

            private JsonResult UploadCurrentChunk(CloudFile model, byte[] chunk, int id)
            {
                using (var chunkStream = new MemoryStream(chunk))
                {
                    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                            string.Format(CultureInfo.InvariantCulture, "{0:D4}", id)));
                    try
                    {
                        model.BlockBlob.PutBlock(
                            blockId,
                            chunkStream, null, null,
                            new BlobRequestOptions()
                            {
                                RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(10), 3)
                            },
                            null);
                        return null;
                    }
                    catch (StorageException e)
                    {
                        Session.Remove("CurrentFile");
                        model.IsUploadCompleted = true;
                        model.UploadStatusMessage = "Failed to Upload file. Exception - " + e.Message;
                        return Json(new { error = true, isLastBlock = false, message = model.UploadStatusMessage });
                    }
                }
            }

        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(Login login)
        {
            string username = ConfigurationManager.AppSettings.Get("Username");
            string userPasscode = ConfigurationManager.AppSettings.Get("UsernamePasscode");
            string adminName = ConfigurationManager.AppSettings.Get("AdminName");
            string adminPasscode = ConfigurationManager.AppSettings.Get("AdminPasscode");

            if ((login.UserName.ToString() != null && login.Password.ToString() == userPasscode) ||
                (login.UserName.ToString() == adminName && login.Password.ToString() == adminPasscode))
            {
                Session["username"] = login.UserName.ToString();
                return RedirectToAction("UploadFile");
            }
            else
            {
                ViewData["Message"] = "User Login Failed";
            }
            return View();
        }

        public static void WriteTableStorage(Upload upload)
        {
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = cloudStorageAccount.CreateCloudTableClient();
            CloudTable cloudTable = tableClient.GetTableReference("samplekinectupload");
            CreateNewTable(cloudTable);

            TableEntity tableEntity = new UploadTableEntity(cloudTable.Name, DateTime.Now.Ticks.ToString(), upload.UserName, upload.FileName, upload.CreateTime, upload.IsUploadCompleted);
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(tableEntity);
            cloudTable.Execute(insertOrMergeOperation);
        }

        

        public static void CreateNewTable(CloudTable table)
        {
            if (!table.CreateIfNotExists())
            {
                Console.WriteLine("Table {0} already exists", table.Name);
                return;
            }
            Console.WriteLine("Table {0} created", table.Name);
        }
    }
}
