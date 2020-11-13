using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace slowapp.Properties
{
    public class SlowController : Controller
    {
        private static readonly string DbConnString = Environment.GetEnvironmentVariable("DB_CONNSTRING_MYSQL");

        [Route("slow/{timeout}")]
        public async Task<IActionResult> SlowGet(int timeout)
        {
            await Task.Delay(timeout);
            return Ok("Heeeelllllooooo Woooooorld!");
        }

        [Route("slow/{timeout}/db")]
        public async Task<IActionResult> SlowDbGet(int timeout)
        {
            if (string.IsNullOrEmpty(DbConnString))
                return BadRequest("Db connection string is not set");
            if (timeout > 100) timeout /= 1000;
            if (timeout == 0)
                return BadRequest("Timeout invalid");
            const string sql = @"SELECT SLEEP(@timeout)";
            var connection = new MySqlConnection(DbConnString);
            var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("timeout", timeout);
            try
            {
                await using (connection)
                {
                    connection.Open();
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                        return Ok("Daaataabaasseee saaayyysss hhhhiiiiii!");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not read from database: {ex.Message}");
            }
            throw new Exception("Slow app did a whoopsie.");
        }

        [Route("slowasync/{timeout}/db")]
        public async Task<IActionResult> SlowAsyncDbGet(int timeout)
        {
            if (string.IsNullOrEmpty(DbConnString))
                return BadRequest("Db connection string is not set");
            if (timeout > 100) timeout /= 1000;
            if (timeout == 0)
                return BadRequest("Timeout invalid");
            const string sql = @"SELECT SLEEP(@timeout)";
            try
            {
                using (var conn = new MySqlConnection(DbConnString))
                {
                    await conn.OpenAsync();

                    // Retrieve all rows
                    using (var cmd = new MySqlCommand(sql, conn)) {
                        cmd.Parameters.AddWithValue("timeout", timeout);

                        using (var reader = await cmd.ExecuteReaderAsync())
                            while (await reader.ReadAsync())
                                return Ok("Daaataabaasseee saaayyysss hhhhiiiiii!");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not read from database: {ex.Message}");
            }
            throw new Exception("Slow app did a whoopsie.");
        }

        [Route("slow/{timeout}")]
        [HttpPost]
        public async Task<IActionResult> SlowPost(int timeout)
        {
            await Task.Delay(timeout);
            return Ok($"Meeeessssaaaaaggeee Reeeeccciiieeevveedd!");
        }
    }
}
