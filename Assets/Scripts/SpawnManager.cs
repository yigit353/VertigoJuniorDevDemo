using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This enum specifies team of the player.
/// </summary>
public enum PlayerTeam
{
    None,
    BlueTeam,
    RedTeam
}

/// <summary>
/// Main class managing spawn behavior for a new player
/// </summary>
public class SpawnManager : MonoBehaviour
{
    [Tooltip("These points are shared between two teams for spawning.")]
    [SerializeField] private List<SpawnPoint> _sharedSpawnPoints = new List<SpawnPoint>();
    [Tooltip("This value will be used to calculate the second filter where the algorithm looks for closest team members. If the friends are away from this value, they will be ignored.")]
    [SerializeField] private float _maxDistanceToClosestFriend = 30;
    [Tooltip("This value will be used to calculate the first filter where the algorithm looks for enemies that are far away from this value. Only enemies which are away from this value will be calculated.")]
    [SerializeField] private float _minDistanceToClosestEnemy = 10;
    [Tooltip("This value is to prevent friendly player spawning on top of each other. If a player is within the range of this value to a spawn point, that spawn point will be ignored.")]
    [SerializeField] private float _minMemberDistance = 2;
    [Tooltip("The dummy player to be spawned by the manager")]
    [SerializeField] private DummyPlayer _playerToBeSpawned;
    [Tooltip("All dummy players in the scene")]
    [SerializeField] private DummyPlayer[] _dummyPlayers;

    // Last spawn point is saved in case that there is no valid point to be spawned.In that case, the player is spawned at the last spawn location.
    // The default behavior has been changed because otherwise the player can be spawned at an invalid location, e.g., SpawnPoint 4.
    private SpawnPoint _lastSpawnPoint = null;

    // For random variable generation
    private System.Random _random = new System.Random();

    private void Awake()
    {
        // Get all spawn points in the scene
        _sharedSpawnPoints.AddRange(FindObjectsOfType<SpawnPoint>());

        // Set the last spawn point as the first instance of SpawnPoint found in the scene.
        // Even if it is not a valid location it will be corrected since at the beginning timers were reset 
        // and only a valid location will be selected and set to this variable.
        _lastSpawnPoint = _sharedSpawnPoints[0];

        // Get all the players of the class DummyPlayer in the scene.
        _dummyPlayers = FindObjectsOfType<DummyPlayer>();
    }

    #region SPAWN ALGORITHM

    /// <summary>
	/// This method calculates a valid spawn point w.r.t. The player team, firstly by the enemy distances and then by the team distances. 
    /// If neither of them is available, this method returns the last spawn location.
    /// If there is more than one valid spawn location, this method returns a random spawn point in the first half of the available points list.
	/// </summary>
    public SpawnPoint GetSharedSpawnPoint(PlayerTeam team)
    {
        // Allocate valid spawn points list with the size of shared spawn points
        // Note: specifying with a constant size was unneccessary because it will be cleared or recreated at the both methods anyway.
        List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

        // Calculate distance of all spawn points w.r.t. friends and enemies of the specified team
        CalculateDistancesForSpawnPoints(team);

        // Try to get available points for the given team and set to spawnPoints list
        GetSpawnPointsByDistanceSpawning(team, ref spawnPoints);

        // If there are no availble points with the first filter...
        if (spawnPoints.Count == 0)
        {
            // Check if there are any available points with the second filter
            GetSpawnPointsBySquadSpawning(team, ref spawnPoints);
        }

        // If there are no available points in both filters that means the timers should be waited.
        // Thus return the last available spawn point to wait until timers are reset.
        if (spawnPoints.Count == 0)
        {
            Debug.Log("Spawn point stays the same");
            return _lastSpawnPoint;
        }

        // If there is only one available spawn location set that one else get a spawn location from the first half because it is more suitable due to distances
        SpawnPoint spawnPoint = spawnPoints.Count <= 1 ? spawnPoints[0] : spawnPoints[_random.Next(0, (int)((float)spawnPoints.Count * .5f))];

        // Reset timer to disable reselection of the same point
        spawnPoint.StartTimer();

        // Set the last point as this available point
        _lastSpawnPoint = spawnPoint;

        return spawnPoint;
    }

    /// <summary>
    /// Calculates a valid spawn point w.r.t. the player team and returns the result by setting reference list suitableSpawnPoints provided.
    /// Suitable points are sorted from the farthest enemy to nearest enemy and returned in that order.
    /// </summary>
    private void GetSpawnPointsByDistanceSpawning(PlayerTeam team, ref List<SpawnPoint> suitableSpawnPoints)
    {
        // If there is a null list provided, create a new one
        if (suitableSpawnPoints == null)
        {
            suitableSpawnPoints = new List<SpawnPoint>();
        }
        // If the list is already populated clear it first
        suitableSpawnPoints.Clear();

        // Sort shared points according to their distance to enemies in descending order
        _sharedSpawnPoints.Sort(delegate (SpawnPoint a, SpawnPoint b)
        {
            if (a.DistanceToClosestEnemy == b.DistanceToClosestEnemy)
            {
                return 0;
            }
            if (a.DistanceToClosestEnemy > b.DistanceToClosestEnemy)
            {
                return -1;
            }
            return 1;
        });

        // Starting from the point being farthest to enemy, firstly check if it its distance from the closest enemy is larger 
        // than the minimum distance to closest enemy
        for (int i = 0; i < _sharedSpawnPoints.Count && _sharedSpawnPoints[i].DistanceToClosestEnemy > _minDistanceToClosestEnemy; i++)
        {

            // Distance to the closest friend and to the closest enemy should be larger than the minimum member distance
            // Also the timer on the spawn point should be past 2 seconds
            if (_sharedSpawnPoints[i].DistanceToClosestFriend > _minMemberDistance && _sharedSpawnPoints[i].DistanceToClosestEnemy > _minMemberDistance && _sharedSpawnPoints[i].SpawnTimer <= 0)
            {
                Debug.Log("Spawn point is created with distance spawning");
                // Suitable point is added to the list
                suitableSpawnPoints.Add(_sharedSpawnPoints[i]);
            }
        }
    }

    /// <summary>
    /// Calculates a valid spawn point w.r.t. the player team and returns the result by setting reference list suitableSpawnPoints provided.
    /// Suitable points are sorted from the nearest team member to farthest team member and returned in that order.
    /// </summary>
    private void GetSpawnPointsBySquadSpawning(PlayerTeam team, ref List<SpawnPoint> suitableSpawnPoints)
    {
        // Note: principles are the same with the GetSpawnPointsByDistanceSpawning method

        if (suitableSpawnPoints == null)
        {
            suitableSpawnPoints = new List<SpawnPoint>();
        }
        suitableSpawnPoints.Clear();
        _sharedSpawnPoints.Sort(delegate (SpawnPoint a, SpawnPoint b)
        {
            if (a.DistanceToClosestFriend == b.DistanceToClosestFriend)
            {
                return 0;
            }
            if (a.DistanceToClosestFriend > b.DistanceToClosestFriend)
            {
                return 1;
            }
            return -1;
        });
        for (int i = 0; i < _sharedSpawnPoints.Count && _sharedSpawnPoints[i].DistanceToClosestFriend <= _maxDistanceToClosestFriend; i++)
        {
            if (!(_sharedSpawnPoints[i].DistanceToClosestFriend <= _minMemberDistance) && !(_sharedSpawnPoints[i].DistanceToClosestEnemy <= _minMemberDistance) && _sharedSpawnPoints[i].SpawnTimer <= 0)
            {
                Debug.Log("Spawn point is created with squad spawning");
                suitableSpawnPoints.Add(_sharedSpawnPoints[i]);
            }
        }
    }

    /// <summary>
    /// Calculates the minimum distances from all shared spawn points to friends and enemies for the specified team
    /// </summary>
    private void CalculateDistancesForSpawnPoints(PlayerTeam playerTeam)
    {
        for (int i = 0; i < _sharedSpawnPoints.Count; i++)
        {
            _sharedSpawnPoints[i].DistanceToClosestFriend = GetDistanceToClosestMember(_sharedSpawnPoints[i].PointTransform.position, playerTeam);
            _sharedSpawnPoints[i].DistanceToClosestEnemy = GetDistanceToClosestMember(_sharedSpawnPoints[i].PointTransform.position, playerTeam == PlayerTeam.BlueTeam ? PlayerTeam.RedTeam : playerTeam == PlayerTeam.RedTeam ? PlayerTeam.BlueTeam : PlayerTeam.None);
        }
    }

    /// <summary>
    /// Calculates the minimum distance to a position, i.e. spawn point from players of the specified team
    /// </summary>
    private float GetDistanceToClosestMember(Vector3 position, PlayerTeam playerTeam)
    {
        // Set the closest distance to positive infinity. This way any valid distance will be smaller than the closest distance
        float _closestDistance = float.PositiveInfinity;

        // For each dummy player...
        foreach (var player in _dummyPlayers)
        {
            // If a player is neither disabled, nor dead, nor without a team and player belongs to the specified team...
            if (!player.Disabled && player.PlayerTeamValue != PlayerTeam.None && player.PlayerTeamValue == playerTeam && !player.IsDead())
            {
                // Calculate the distance from the specified position to the player
                float playerDistanceToSpawnPoint = Vector3.Distance(position, player.Transform.position);

                // If the distance is less than the closest distance, set the distance as the closest distance thus calculating the closest distance
                // at the end of the loop
                if (playerDistanceToSpawnPoint < _closestDistance)
                {
                    _closestDistance = playerDistanceToSpawnPoint;
                }
            }
        }
        return _closestDistance;
    }

    #endregion

    /// <summary>
    /// Gets the most suitable spawn point to the specified player in the editor for test purposes.
    /// and sets the position of the player as that spawn point's position. 
    /// </summary>
    public void TestGetSpawnPoint()
    {
        SpawnPoint spawnPoint = GetSharedSpawnPoint(_playerToBeSpawned.PlayerTeamValue);
        _playerToBeSpawned.Transform.position = spawnPoint.PointTransform.position;
    }

}