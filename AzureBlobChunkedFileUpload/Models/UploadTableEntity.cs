using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AzureBlobChunkedFileUpload.Models
{
    public class UploadTableEntity : TableEntity
    {
        public UploadTableEntity() { }
        // needed as per: https://stackoverflow.com/questions/53405731/retrieving-row-from-azure-table-storage
        public UploadTableEntity(string part, string row, string username, string filename, DateTime createtime, bool complete)
        {
            this.PartitionKey = part;
            this.RowKey = row;
            this.UserName = username;
            this.FileName = filename;
            this.CreateTime = createtime;
            this.IsComplete = complete;
        }

        public string UserName { get; set; }
        public string FileName { get; set; }
        public DateTime CreateTime { get; set; }
        public bool IsComplete { get; set; }

    }
}