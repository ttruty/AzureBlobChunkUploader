using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace AzureBlobChunkedFileUpload.Models
{
    public class Login
    {
        [Required(ErrorMessage = "Please enter your username")]
        [Display(Name = "Enter Username :")]
        public string UserName { get; set; }
        [Required(ErrorMessage = "Please enter your password")]
        [Display(Name = "Enter Password :")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}