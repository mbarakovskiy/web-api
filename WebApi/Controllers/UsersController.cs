using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Game.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : Controller
    {
        private readonly IUserRepository userRepository;

        private readonly IMapper mapper;
        
        private readonly LinkGenerator linkGenerator;
        // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
        public UsersController(IUserRepository userRepository,
            IMapper mapper, LinkGenerator linkGenerator)
        {
            this.userRepository = userRepository;
            this.mapper = mapper;
            this.linkGenerator = linkGenerator;
        }

        [HttpGet("{userId}", Name = nameof(GetUserById))]
        [HttpHead("{userId}")]
        [Produces("application/json", "application/xml")]
        public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
        {
            var user = userRepository.FindById(userId);
            if (user == null)
            {
                return NotFound();
            }
            var resultUser = mapper.Map<UserDto>(user);
            return Ok(resultUser);
        }

        [HttpPost]
        [Produces("application/json", "application/xml")]
        public IActionResult CreateUser([FromBody] UserPostDTO userPostDto)
        {
            if (!ModelState.IsValid)
            {
                if (ModelState.ContainsKey("Login"))
                    return UnprocessableEntity(ModelState);
                return new BadRequestResult();
            }
            foreach (var letter in userPostDto.Login.Where(letter => !char.IsLetterOrDigit(letter)))
            {
                ModelState.AddModelError("Login", "Сообщение об ошибке");
            }
            if (!ModelState.IsValid)
            {
                return UnprocessableEntity(ModelState);
            }
            var user = mapper.Map<UserEntity>(userPostDto);
            var createdUserEntity = userRepository.Insert(user);
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = createdUserEntity.Id },
                createdUserEntity.Id);
        }

        [HttpPut("{userId}", Name = nameof(UpdateUser))]
        [Produces("application/json", "application/xml")]
        public IActionResult UpdateUser([FromRoute] Guid userId, [FromBody] UserPutDTO userPutDto)
        {
            if (userId.Equals(Guid.Empty) || userPutDto == null)
            {
                return BadRequest();
            }
            if (!ModelState.IsValid)
            {
                return UnprocessableEntity(ModelState);
            }

            userPutDto.Id = userId;
            var user = mapper.Map<UserEntity>(userPutDto);
            userRepository.UpdateOrInsert(user, out var isInserted);
            if (!isInserted)
            {
                return NoContent();
            }

            return CreatedAtRoute(nameof(UpdateUser),
                new {userId = user.Id},
                user.Id);
        }
        
        [HttpPatch("{userId}", Name = nameof(PartiallyUpdateUser))]
        [Produces("application/json", "application/xml")]
        public IActionResult PartiallyUpdateUser([FromRoute] Guid userId, [FromBody] JsonPatchDocument<UserPutDTO> patchDoc)
        {
            if (patchDoc == null)
            {
                return BadRequest();
            }

            var foundUserEntity = userRepository.FindById(userId);
            if (foundUserEntity == null)
                return NotFound();
            
            var user = mapper.Map<UserPutDTO>(foundUserEntity);
            patchDoc.ApplyTo(user, ModelState);

            if (!TryValidateModel(user))
                return UnprocessableEntity(ModelState);

            foundUserEntity = mapper.Map(user, foundUserEntity);

            userRepository.Update(foundUserEntity);
            return NoContent();
        }

        [HttpDelete("{userId}", Name = nameof(DeleteUser))]
        [Produces("application/json", "application/xml")]
        public IActionResult DeleteUser([FromRoute] Guid userId)
        {
            if (userRepository.FindById(userId) == null)
            {
                return NotFound();
            }
            userRepository.Delete(userId);
            return NoContent();
        }
        
        [HttpGet(Name = nameof(GetUsers))]
        [Produces("application/json", "application/xml")]
        public IActionResult GetUsers([FromQuery] int pageNumber=1, [FromQuery] int pageSize=10)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Min(Math.Max(1, pageSize), 20);
            var pageList = userRepository.GetPage(pageNumber, pageSize);
            var users = mapper.Map<IEnumerable<UserDto>>(pageList);
            string previousPageLink = null;
            if (pageNumber > 1)
            {
                previousPageLink = linkGenerator.GetUriByRouteValues(HttpContext, "GetUsers", new { pageNumber = pageNumber - 1, pageSize });
            }
            var paginationHeader = new
            {
                previousPageLink,
                nextPageLink = linkGenerator.GetUriByRouteValues(HttpContext, "GetUsers", new { pageNumber = pageNumber + 1, pageSize }),
                totalCount = 1,
                pageSize,
                currentPage = pageNumber,
                totalPages = 1,
            };
            Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
            return Ok(users);
        }

        [HttpOptions(Name = nameof(GetOptions))]
        [Produces("application/json", "application/xml")]
        public IActionResult GetOptions()
        {
            Response.Headers.Add("Allow", new []{"POST", "GET", "OPTIONS"});
            return Ok();
        }
    }
}