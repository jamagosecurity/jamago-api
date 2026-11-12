using System.Net;
using System.Text.Json;
using Jama.Infrastructure.Flights;

namespace Jama.Application.Tests;

public class FlightFeedTests
{
    private const string Sample = """
        {"time":1,"states":[
          ["06a1a2","QTR839  ","Qatar",1,1,51.657,25.139,678.18,false,90.5,337.9,0,null,700,"1234",false,0],
          ["06a1a3","","Qatar",1,1,51.561,25.274,null,true,0,244.69,0,null,null,null,false,0],
          ["06a1a4","NOPOS","Qatar",1,1,null,null,null,false,null,null,0,null,null,null,false,0]
        ]}
        """;

    private sealed class Counting(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }

    [Fact]
    public void Aircraft_are_read_and_rows_without_a_position_are_skipped()
    {
        var flights = OpenSkyFlightFeed.Parse(JsonDocument.Parse(Sample));
        Assert.Equal(2, flights.Count);
        Assert.Equal("QTR839", flights[0].Callsign);
        Assert.Equal(25.139, flights[0].Latitude);
        Assert.Equal(337.9, flights[0].HeadingDegrees);
        Assert.True(flights[1].OnGround);
        Assert.Null(flights[1].AltitudeMetres);
    }

    [Fact]
    public async Task One_fetch_serves_everyone_for_45_seconds()
    {
        var handler = new Counting(HttpStatusCode.OK, Sample); var clock = new Clock();
        var feed = new OpenSkyFlightFeed(new HttpClient(handler), clock);

        await feed.GetQatarAsync(default); await feed.GetQatarAsync(default); await feed.GetQatarAsync(default);
        Assert.Equal(1, handler.Calls);

        clock.Now = clock.Now.AddSeconds(46);
        await feed.GetQatarAsync(default);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task When_the_source_fails_the_last_good_list_is_kept_and_nothing_throws()
    {
        var good = new Counting(HttpStatusCode.OK, Sample); var clock = new Clock();
        var feed = new OpenSkyFlightFeed(new HttpClient(good), clock);
        Assert.Equal(2, (await feed.GetQatarAsync(default)).Count);

        var limited = new OpenSkyFlightFeed(new HttpClient(new Counting(HttpStatusCode.TooManyRequests, "")), clock);
        Assert.Empty(await limited.GetQatarAsync(default));                  // nothing yet, and no exception

        var broken = new OpenSkyFlightFeed(new HttpClient(new Counting(HttpStatusCode.OK, "not json")), clock);
        Assert.Empty(await broken.GetQatarAsync(default));
    }
}
