We created a slowapp to experiment with startup of pods. We used the asp.net version of the app to play with slow database connections using sync/async database connections and the [MySQL.Data](https://dev.mysql.com/doc/connector-net) and [MySqlConnector](https://mysqlconnector.net/) packages. We also changed the thread pool settings for the app.

## A slow database connection

We're firing 1 batch of 130 concurrent requests at a 'cold' app (recently deployed, no requests made to it) that uses the database with a 5sec delayed database response (using `SELECT SLEEP`)...

```sh
kubectl rollout restart deploy slowapp-aspnet -n dev
hey -n 130 -c 130 -t 0 http://slowapp-aspnet.127.0.0.1.xip.io/slow/5/db

Response time histogram:
  11.707 [1]  |■■
  14.499 [2]  |■■■■
  17.292 [5]  |■■■■■■■■■
  20.084 [8]  |■■■■■■■■■■■■■■■
  22.876 [14] |■■■■■■■■■■■■■■■■■■■■■■■■■
  25.669 [11] |■■■■■■■■■■■■■■■■■■■■
  28.461 [14] |■■■■■■■■■■■■■■■■■■■■■■■■■
  31.254 [15] |■■■■■■■■■■■■■■■■■■■■■■■■■■■
  34.046 [20] |■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
  36.838 [22] |■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
  39.631 [18] |■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

Status code distribution:
  [200] 130 responses
```

Yuck. Establishing database connections is slow.

But now that we've warmed the connection pool, try again while connections are still in the pool:

```sh
hey -n 130 -c 130 -t 0 http://slowapp-aspnet.127.0.0.1.xip.io/slow/5/db

Response time histogram:
  14.411 [1]  |■
  14.990 [77] |■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
  15.569 [34] |■■■■■■■■■■■■■■■■■■
  16.148 [1]  |■
  16.727 [1]  |■
  17.307 [1]  |■
  17.886 [1]  |■
  18.465 [1]  |■
  19.044 [0]  |
  19.624 [1]  |■
  20.203 [12] |■■■■■■

Status code distribution:
  [200] 130 responses
```

An improvement, as some of the connections are reused within their `ConnectionTimeout` limit.

And again and again, after a few iterations, each time improving with more available pooled connections, until we have a fully warmed connection pool...:

```sh
hey -n 130 -c 130 -t 0 http://slowapp-aspnet.127.0.0.1.xip.io/slow/5/db

Response time histogram:
  5.117 [1] |■
  5.637 [77]  |■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
  6.158 [0] |
  6.678 [0] |
  7.199 [0] |
  7.719 [0] |
  8.240 [0] |
  8.760 [0] |
  9.281 [0] |
  9.801 [0] |
  10.322 [52] |■■■■■■■■■■■■■■■■■■■■■■■■■■■

Status code distribution:
  [200] 130 responses
```

So, we have 100 max connections, and requests being fulfilled on multiples of 5secs. The first 100 ~5secs, then the last 30 at ~10secs. Makes sense.

### Wait for connections to be closed (15 secs of no use):

```sh
hey -n 130 -c 130 -t 0 http://slowapp-aspnet.127.0.0.1.xip.io/slow/5/db

Response time histogram:
  35.207 [1]  |
  35.707 [111]  |■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
  36.206 [2]  |■
  36.706 [2]  |■
  37.206 [1]  |
  37.705 [1]  |
  38.205 [4]  |■
  38.704 [1]  |
  39.204 [4]  |■
  39.704 [1]  |
  40.203 [2]  |■

Status code distribution:
  [200] 130 responses
```

Back to Slowzville.

## Conclusions...

After playing around with various MySQL connector packages and other settings...

The MySQL.Data connector package used by the ASP.NET app, (https://dev.mysql.com/doc/connector-net) maintains a pool of connections (`MaximumPoolSize` default 100). Establishing connections is slow, and they timeout after 15 secs of inactivity (`ConnectionTimeout` default 15 secs).

If more than 100 concurrent requests are made, they are queued until a connections are available. In theory, for 300 concurrent requests to an endpoint that takes 5 secs to respond we see 100 requests filled in 5 secs, the next 100 in 10 secs and the last 100 in 15 secs. This is true if the connection pool is warmed up.

However, if the connection pool is cold (no active connections in the last 15 secs), connections have to be reestablished first, and this is slow. So slow that with enough concurrent requests we may see timeouts at the nginx gateway (60 secs for this demo).

### Async?

What about async? If we change the endpoint to use async connection how does that change things... For the MySQL.Data connector, not at all. That correlates with what the MySqlConnector documentation says; "Async calls map to synchronous I/O" for the MySQL.Data connector. So, we see the same cold-start problem. However, if we use the MySqlConnector (https://mysqlconnector.net/) instead, we see an immediate improvement. Requests are fulfilled in multiples of 5 secs, even from a cold start.

### Threads...

What about changing the thread pool for the server?

```asp
  public static void Main(string[] args)
  {
      // From https://github.com/aspnet/KestrelHttpServer/issues/2104
      System.Net.ServicePointManager.DefaultConnectionLimit = 256;
      System.Threading.ThreadPool.GetMaxThreads(out int _, out int completionThreads);
      System.Threading.ThreadPool.SetMinThreads(1024, completionThreads);
      CreateHostBuilder(args).Build().Run();
  }
```

Back to the original connector package (MySQL.Data), and a synchronous connection... With the above thread pool configuration in place, we see a similar response to requests from a cold start as for the async requests with the MySqlConnector package, with ~1sec extra delay to establish the connection. So, 100 requests fullfilled in ~6secs (5sec + 1sec), then multiples of 5secs there after. This is similar performance to using the async connection.

So, we can either use async connections with the MySqlConnector or the MySQL.Data connector and the increase the max threads available, as above.
