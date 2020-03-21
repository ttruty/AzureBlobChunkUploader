using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AzureBlobChunkedFileUpload.Models
{
    public class Upload
    {
        public string UserName { get; set; }
        public string FileName { get; set; }
        public DateTime CreateTime { get; set; }
        public bool IsUploadCompleted { get; set; }
    }
}