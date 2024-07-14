using GpsUtil.Location;
using Microsoft.AspNetCore.Mvc;
using TourGuide.Services;
using TourGuide.Services.Interfaces;
using TourGuide.Users;
using TripPricer;

namespace TourGuide.Controllers;

[ApiController]
[Route("[controller]")]
public class TourGuideController : ControllerBase
{
    private readonly ITourGuideService _tourGuideService;
    private readonly IRewardsService _rewardsService;

    public TourGuideController(ITourGuideService tourGuideService, IRewardsService rewardsService)
    {
        _tourGuideService = tourGuideService;
        _rewardsService = rewardsService;
    }

    [HttpGet("getLocation")]
    public ActionResult<VisitedLocation> GetLocation([FromQuery] string userName)
    {
        var location = _tourGuideService.GetUserLocation(GetUser(userName));
        return Ok(location);
    }

    // TODO: Change this method to no longer return a List of Attractions.
    // Instead: Get the closest five tourist attractions to the user - no matter how far away they are.
    // Return a new JSON object that contains:
    // Name of Tourist attraction, 
    // Tourist attractions lat/long, 
    // The user's location lat/long, 
    // The distance in miles between the user's location and each of the attractions.
    // The reward points for visiting each Attraction.
    //    Note: Attraction reward points can be gathered from RewardsCentral
    [HttpGet("getNearbyAttractions")]
    public ActionResult<IEnumerable<object>> GetNearbyAttractions([FromQuery] string userName)
    {
        // Retrieve the user
        User user = _tourGuideService.GetUser(userName);
        if (user == null)
        {
            return NotFound($"User '{userName}' not found.");
        }

        // Retrieve the user's last visited location
        VisitedLocation visitedLocation = _tourGuideService.GetUserLocation(user);

        // Retrieve the closest attractions
        List<Attraction> closestAttractions = _tourGuideService.GetNearByAttractions(visitedLocation);

        // Create the result object
        var result = closestAttractions.Select(attraction => new
        {
            AttractionName = attraction.AttractionName,
            AttractionLocation = new
            {
                Latitude = attraction.Latitude,
                Longitude = attraction.Longitude
            },
            UserLocation = new
            {
                Latitude = visitedLocation.Location.Latitude,
                Longitude = visitedLocation.Location.Longitude
            },
            Distance = _rewardsService.GetDistance(attraction, visitedLocation.Location),
            RewardPoints = _rewardsService.GetRewardPoints(attraction, user)
        });

        return Ok(result);
    }

    [HttpGet("getRewards")]
    public ActionResult<List<UserReward>> GetRewards([FromQuery] string userName)
    {
        var rewards = _tourGuideService.GetUserRewards(GetUser(userName));
        return Ok(rewards);
    }

    [HttpGet("getTripDeals")]
    public ActionResult<List<Provider>> GetTripDeals([FromQuery] string userName)
    {
        var deals = _tourGuideService.GetTripDeals(GetUser(userName));
        return Ok(deals);
    }

    private User GetUser(string userName)
    {
        return _tourGuideService.GetUser(userName);
    }
}
