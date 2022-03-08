using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
 
namespace searching.Models
{
    public class Search
    {
        [Required(ErrorMessage = "Enter your search query")]
        public string search_text { get;set; }
    }
}