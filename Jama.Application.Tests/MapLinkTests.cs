using Jama.Application.Geo;
using Jama.Infrastructure.Geo;

namespace Jama.Application.Tests;

public class MapLinkTests
{
    [Theory]
    [InlineData("https://www.google.com/maps/place/Katara/@25.3606,51.5256,17z/data=!3m1!4b1!4m6!3m5!1s0x3e45:0x1!8m2!3d25.3600123!4d51.5255999!16s", 25.360012, 51.5256)]   // the exact place beats the view's centre
    [InlineData("https://www.google.com/maps/place/Doha/@25.2854473,51.5310398,13z/data=!3m1!4b1", 25.285447, 51.53104)]
    [InlineData("https://www.google.com/maps?q=25.286106,51.534817", 25.286106, 51.534817)]
    [InlineData("https://maps.google.com/?ll=25.286106,51.534817&z=15", 25.286106, 51.534817)]
    [InlineData("https://www.google.com/maps/place/25.286106,51.534817", 25.286106, 51.534817)]
    [InlineData("https://www.google.com/maps/search/?api=1&query=25.286106%2C51.534817", 25.286106, 51.534817)]
    public void A_pin_is_read_out_of_the_usual_maps_links(string url, double latitude, double longitude)
    {
        var pin = MapLinks.TryParse(url);
        Assert.NotNull(pin);
        Assert.Equal(latitude, pin.Latitude, 5);
        Assert.Equal(longitude, pin.Longitude, 5);
    }

    [Theory]
    [InlineData("https://maps.app.goo.gl/AbCdEf123")]
    [InlineData("hello")]
    [InlineData("https://www.google.com/maps/@0,0,3z")]          // (0, 0) is a mistake, never a site
    [InlineData("https://www.google.com/maps/@95.1,51.5,3z")]    // out of range
    public void Links_with_no_usable_pin_give_nothing(string url) => Assert.Null(MapLinks.TryParse(url));

    [Theory]
    [InlineData("maps.app.goo.gl", true)]
    [InlineData("goo.gl", true)]
    [InlineData("www.google.com", true)]
    [InlineData("maps.google.com", true)]
    [InlineData("www.google.com.qa", true)]
    [InlineData("google.com.evil.com", false)]
    [InlineData("maps.app.goo.gl.evil.com", false)]
    [InlineData("evil.com", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("localhost", false)]
    [InlineData("", false)]
    public void Only_google_maps_hosts_may_be_followed(string host, bool allowed) => Assert.Equal(allowed, MapLinks.IsAllowedHost(host));

    private sealed class Redirects(Dictionary<string, string> map) : HttpMessageHandler
    {
        public List<string> Requested { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!.ToString());
            var response = new HttpResponseMessage(map.TryGetValue(request.RequestUri.ToString(), out var next)
                ? System.Net.HttpStatusCode.Found : System.Net.HttpStatusCode.OK);
            if (next is not null) response.Headers.Location = new Uri(next);
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task A_short_share_link_is_followed_to_the_pin()
    {
        var handler = new Redirects(new() { ["https://maps.app.goo.gl/abc"] = "https://www.google.com/maps/place/Pearl/@25.371,51.55,17z" });
        var pin = await new MapLinkResolver(new HttpClient(handler)).ResolveAsync("https://maps.app.goo.gl/abc", default);

        Assert.Equal(new MapPinDto(25.371, 51.55), pin);
        Assert.Single(handler.Requested);
    }

    [Fact]
    public async Task A_redirect_to_anywhere_but_google_is_never_requested()
    {
        var handler = new Redirects(new() { ["https://maps.app.goo.gl/abc"] = "http://169.254.169.254/latest/meta-data" });
        var pin = await new MapLinkResolver(new HttpClient(handler)).ResolveAsync("https://maps.app.goo.gl/abc", default);

        Assert.Null(pin);
        Assert.Equal(["https://maps.app.goo.gl/abc"], handler.Requested);     // the second address was never called
    }

    [Theory]
    [InlineData("http://maps.app.goo.gl/abc")]                // not https
    [InlineData("https://evil.com/maps/@25.3,51.5,17z")]      // not a Google host, even though it carries coordinates
    [InlineData("file:///etc/passwd")]
    public async Task Links_that_are_not_google_https_are_refused_without_any_request(string url)
    {
        var handler = new Redirects([]);
        Assert.Null(await new MapLinkResolver(new HttpClient(handler)).ResolveAsync(url, default));
        Assert.Empty(handler.Requested);
    }

    [Fact]
    public async Task A_loop_of_redirects_gives_up()
    {
        var handler = new Redirects(new() { ["https://maps.app.goo.gl/a"] = "https://maps.app.goo.gl/b", ["https://maps.app.goo.gl/b"] = "https://maps.app.goo.gl/a" });
        Assert.Null(await new MapLinkResolver(new HttpClient(handler)).ResolveAsync("https://maps.app.goo.gl/a", default));
        Assert.True(handler.Requested.Count <= 6);
    }
}
