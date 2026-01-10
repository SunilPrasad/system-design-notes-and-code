using Microsoft.AspNetCore.Mvc;

namespace MiniRedis.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CacheController : ControllerBase
    {
        private readonly KeyValueStore _store;


        public CacheController(KeyValueStore store)
        {
            _store = store;
        }


        [HttpGet("{key}")]
        public IActionResult Get(string key)
        {
            var value = _store.Get(key);

            if (value == null)
            {
                return NotFound($"Key '{key}' not found.");
            }

            return Ok(value);
        }


        [HttpPost]
        public IActionResult Set([FromBody] KeyValueItem item)
        {
            _store.Set(item.Key, item.Value);
            return Ok($"Key '{item.Key}' saved.");
        }
    }

    public class KeyValueItem
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}