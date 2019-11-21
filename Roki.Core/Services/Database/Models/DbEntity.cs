using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    public class DbEntity
    {
        [Key] 
        [Column("id")]
        public int Id { get; set; }
    }
}