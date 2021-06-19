using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using IdempotentAPI.Filters;
using WebApi_5_0.DTOs;

namespace WebApi_5_0.Controllers
{
    [ApiController]    
    [Route("[controller]")]
    //[Consumes("application/json")]
    //[Produces("application/json")]
    [Idempotent(Enabled = true)]
    public class SimpleController : ControllerBase
    {

        private readonly ILogger<SimpleController> _logger;

        public SimpleController(ILogger<SimpleController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IEnumerable<SimpleResponse> Get()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new SimpleResponse
            {
                Id = rng.Next(1, 1000),
                CreatedOn = DateTime.Now,
                Message = $"A Simple string message: {index}"
            })
            .ToArray();
        }

        [HttpGet("{id}", Name = "GetById")]
        public SimpleResponse GetById(int id)
        {
            return new SimpleResponse()
            {
                Id = id,
                CreatedOn = DateTime.Now,
                Message = $"A Simple string message!"
            };
        }

        [HttpPost]
        public IActionResult Post([FromBody] SimpleRequest simpleRequest)
        {
            // Perform some simple input validations
            if (simpleRequest is null)
            {
                throw new ArgumentNullException(nameof(simpleRequest));
            }

            if (string.IsNullOrEmpty(simpleRequest.Message))
            {
                return BadRequest($"The request message should not be null (Error created at: {DateTime.Now.ToString("s")})");
            }

            // ...Let's assume that we have created an entity in a persistanct storage (e.g. in our database).
            var rng = new Random();
            SimpleResponse simpleResponse = new SimpleResponse()
            {
                Id = rng.Next(1000, 5000),
                CreatedOn = DateTime.Now,
                Message = $"A Simple string message (as created)!"
            };

            return Ok(simpleResponse);
            //return CreatedAtRoute("GetById", new { Id = simpleResponse.Id }, simpleResponse);
        }




    }
}
