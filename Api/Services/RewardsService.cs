﻿using GpsUtil.Location;
using System;
using System.Collections.Concurrent;
using TourGuide.LibrairiesWrappers.Interfaces;
using TourGuide.Services.Interfaces;
using TourGuide.Users;
using System.Threading;

namespace TourGuide.Services;

public class RewardsService : IRewardsService
{
    private const double StatuteMilesPerNauticalMile = 1.15077945;
    private readonly int _defaultProximityBuffer = 10;
    private int _proximityBuffer;
    private readonly int _attractionProximityRange = 200;
    private readonly IGpsUtil _gpsUtil;
    private readonly IRewardCentral _rewardsCentral;
    private static int count = 0;

    public RewardsService(IGpsUtil gpsUtil, IRewardCentral rewardCentral)
    {
        _gpsUtil = gpsUtil;
        _rewardsCentral = rewardCentral;
        _proximityBuffer = _defaultProximityBuffer;
    }

    public void SetProximityBuffer(int proximityBuffer)
    {
        _proximityBuffer = proximityBuffer;
    }

    public void SetDefaultProximityBuffer()
    {
        _proximityBuffer = _defaultProximityBuffer;
    }

     public async Task CalculateRewards(User user)
     {
         count++;
         List<VisitedLocation> userLocations = user.VisitedLocations;
         List<Attraction> attractions = _gpsUtil.GetAttractions();

         // Utiliser un HashSet pour vérifier rapidement si une récompense a déjà été ajoutée
         var attractionNamesWithRewards = new HashSet<string>(user.UserRewards.Select(r => r.Attraction.AttractionName));

         var newRewards = new ConcurrentBag<UserReward>();

         // Utiliser une liste de tâches pour paralléliser les boucles
         var outerTasks = new List<Task>();


          foreach (var visitedLocation in userLocations)
          {
              outerTasks.Add(Task.Run(async () =>
              {
                  var innerTasks = new List<Task>();

                  foreach (var attraction in attractions)
                  {
                      innerTasks.Add(Task.Run(() =>
                      {
                          if (!attractionNamesWithRewards.Contains(attraction.AttractionName))
                          {
                              if (NearAttraction(visitedLocation, attraction))
                              {
                                  var reward = new UserReward(visitedLocation, attraction, GetRewardPoints(attraction, user));
                                  newRewards.Add(reward);
                                  attractionNamesWithRewards.Add(attraction.AttractionName);
                              }
                          }
                      }));
                  }

                  await Task.WhenAll(innerTasks);
              }));
          }

         // Attendre que toutes les tâches extérieures soient terminées
          await Task.WhenAll(outerTasks);

         // Ajouter toutes les nouvelles récompenses après avoir terminé l'énumération
         foreach (var reward in newRewards)
         {
             user.AddUserReward(reward);
         }
     }

    /*  public void CalculateRewards(User user)
      {
          count++;
          List<VisitedLocation> userLocations = user.VisitedLocations;
          List<Attraction> attractions = _gpsUtil.GetAttractions();

          // Utiliser un HashSet pour vérifier rapidement si une récompense a déjà été ajoutée
          var attractionNamesWithRewards = new HashSet<string>(user.UserRewards.Select(r => r.Attraction.AttractionName));

          var newRewards = new ConcurrentBag<UserReward>();


          List<Thread> threads = new List<Thread>();

          foreach (var visitedLocation in userLocations)
          {
              threads.Add(new Thread(new ThreadStart(() =>
          {
              foreach (var attraction in attractions)
              {

                  if (!attractionNamesWithRewards.Contains(attraction.AttractionName))
                  {
                      if (NearAttraction(visitedLocation, attraction))
                      {
                          var reward = new UserReward(visitedLocation, attraction, GetRewardPoints(attraction, user));
                          newRewards.Add(reward);
                          attractionNamesWithRewards.Add(attraction.AttractionName);
                      }
                  }
              }
          })));
          }
          foreach (Thread thread in threads)
          {
              thread.Start();
          }
          foreach (Thread thread in threads)
          {
              thread.Join();
          }

          // Ajouter toutes les nouvelles récompenses après avoir terminé l'énumération
          foreach (var reward in newRewards)
          {
              user.AddUserReward(reward);
          }
      }
 */
  /*  // Fonctionne mais pas très performant
    public void CalculateRewards(User user)
    {
        count++;
        List<VisitedLocation> userLocations = user.VisitedLocations;
        List<Attraction> attractions = _gpsUtil.GetAttractions();

        // Utiliser un HashSet pour vérifier rapidement si une récompense a déjà été ajoutée
        var attractionNamesWithRewards = new HashSet<string>(user.UserRewards.Select(r => r.Attraction.AttractionName));

        var newRewards = new ConcurrentBag<UserReward>();

        List<Thread> threads = userLocations.Select(visitedLocation =>
        {
            return new Thread(() =>
            {
                Parallel.ForEach(attractions, attraction =>
                {
                    if (!attractionNamesWithRewards.Contains(attraction.AttractionName))
                    {
                        if (NearAttraction(visitedLocation, attraction))
                        {
                            var reward = new UserReward(visitedLocation, attraction, GetRewardPoints(attraction, user));
                            newRewards.Add(reward);

                            lock (attractionNamesWithRewards)
                            {
                                attractionNamesWithRewards.Add(attraction.AttractionName);
                            }
                        }
                    }
                });
            });
        }).ToList();

        // Démarrer tous les threads
        foreach (var thread in threads)
        {
            thread.Start();
        }

        // Attendre que tous les threads se terminent
        foreach (var thread in threads)
        {
            thread.Join();
        }

        // Ajouter toutes les nouvelles récompenses après avoir terminé l'énumération
        foreach (var reward in newRewards)
        {
            user.AddUserReward(reward);
        }
    }*/


    // Méthode utilitaire pour diviser une liste en sous-listes
    private List<List<T>> SplitList<T>(List<T> list, int chunkSize)
    {
        var result = new List<List<T>>();
        for (int i = 0; i < list.Count; i += chunkSize)
        {
            result.Add(list.Skip(i).Take(chunkSize).ToList());
        }
        return result;
    }

    public bool IsWithinAttractionProximity(Attraction attraction, Locations location)
    {
        Console.WriteLine(GetDistance(attraction, location));
        return GetDistance(attraction, location) <= _attractionProximityRange;
    }

    private bool NearAttraction(VisitedLocation visitedLocation, Attraction attraction)
    {
        return GetDistance(attraction, visitedLocation.Location) <= _proximityBuffer;
    }

    public int GetRewardPoints(Attraction attraction, User user)
    {
        return _rewardsCentral.GetAttractionRewardPoints(attraction.AttractionId, user.UserId);
    }

    public double GetDistance(Locations loc1, Locations loc2)
    {
        double lat1 = Math.PI * loc1.Latitude / 180.0;
        double lon1 = Math.PI * loc1.Longitude / 180.0;
        double lat2 = Math.PI * loc2.Latitude / 180.0;
        double lon2 = Math.PI * loc2.Longitude / 180.0;

        double angle = Math.Acos(Math.Sin(lat1) * Math.Sin(lat2)
                                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(lon1 - lon2));

        double nauticalMiles = 60.0 * angle * 180.0 / Math.PI;
        return StatuteMilesPerNauticalMile * nauticalMiles;
    }
}
