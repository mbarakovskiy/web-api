using System.ComponentModel.DataAnnotations;

namespace WebApi.Controllers
{
    public class UserPostDTO
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        [Required] 
        public string Login { get; set; }
    }
    
}