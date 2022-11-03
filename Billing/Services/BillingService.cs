using System;
using System.Linq;
using Billing.Models;
using Grpc.Core;
using Newtonsoft.Json;

namespace Billing.Services
{
    public class BillingService : Billing.BillingBase
    {
        private readonly ILogger<BillingService> _logger;
        private readonly List<User> _users;
        private Dictionary<Coin, User> _coins;


        public BillingService(ILogger<BillingService> logger)
        {
            _logger = logger;
            _coins = new Dictionary<Coin, User>();

            using (StreamReader file = File.OpenText(@"Static\Users.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                _users = (List<User>)serializer.Deserialize(file, typeof(List<User>));
            }

            foreach (var user in _users)
            {
                user.Profile = new UserProfile() {Name = user.Name};
            }
        }

        public override async Task ListUsers(None request, IServerStreamWriter<UserProfile> responseStream, ServerCallContext context)
        {
            foreach (var user in _users)
            {
                await responseStream.WriteAsync(user.Profile);
            }
        }

        public override Task<Response> CoinsEmission(EmissionAmount request, ServerCallContext context)
        {
            Response output = new Response();

            if (request.Amount < _users.Count)
            {
                output.Status = Response.Types.Status.Failed;
                output.Comment = "Not enough coins";
                return Task.FromResult(output);
            }

            DistributeCoins(request.Amount);
            output.Status = Response.Types.Status.Ok;
            output.Comment = "Coins are distributed";

            foreach (var coin in _coins)
            {
                Console.WriteLine(coin.Key + " " + coin.Value.Profile.Amount);
            }
            return Task.FromResult(output);
        }

        public override Task<Response> MoveCoins(MoveCoinsTransaction request, ServerCallContext context)
        {
            Response output = new Response();
            User srcUser = _users.FirstOrDefault(user => user.Name == request.SrcUser);
            User dstUser = _users.FirstOrDefault(user => user.Name == request.DstUser);
            long amount = request.Amount;

            if (srcUser == null)
            {
                output.Status = Response.Types.Status.Failed;
                output.Comment = "srcUser not found";
                return Task.FromResult(output);
            }

            if (dstUser == null)
            {
                output.Status = Response.Types.Status.Failed;
                output.Comment = "dstUser not found";
                return Task.FromResult(output);
            }

            if (amount < 0)
            {
                output.Status = Response.Types.Status.Failed;
                output.Comment = "Money less zero";
                return Task.FromResult(output);
            }

            if (amount > srcUser.Profile.Amount)
            {
                output.Status = Response.Types.Status.Failed;
                output.Comment = "srcUser does not have enough money";
                return Task.FromResult(output);
            }

            MoveCoins(amount, srcUser, dstUser);

            foreach (var coin in _coins)
            {
                Console.WriteLine(coin.Key.Id +" "+ coin.Key.History + " " + coin.Value.Profile.Amount);
            }
            output.Status = Response.Types.Status.Ok;
            output.Comment = "money is moved";
            return Task.FromResult(output);

        }

        public override Task<Coin> LongestHistoryCoin(None request, ServerCallContext context)
        {
            Coin output = _coins.OrderByDescending(coin => coin.Key.History.Split('\n').Length).FirstOrDefault().Key;
            return Task.FromResult(output);
        }

        private void DistributeCoins(long amount)
        {
            double currentCoins;
            double usersRating = 0;
            double distributedCoins = 0;
            double sumRating = _users.Sum(user => user.Rating);
            double coefficient = amount / sumRating;
            List<User> mostRatingUsers = new List<User>();

            foreach (var user in _users)
            {
                usersRating = user.Rating;
                currentCoins = Math.Round(usersRating * coefficient - distributedCoins);
                if (currentCoins < 1)
                {
                    currentCoins = 1;
                    CreateCoin((long)currentCoins, user);
                    distributedCoins += currentCoins;

                }
                else
                {
                    mostRatingUsers.Add(user);                    
                }
            }

            coefficient = (amount - distributedCoins) / sumRating;
            usersRating = 0;
            distributedCoins = 0;

            foreach (var user in mostRatingUsers)
            {
                usersRating += user.Rating;
                currentCoins = Math.Round(usersRating * coefficient - distributedCoins);
                distributedCoins += currentCoins;
                CreateCoin((long)currentCoins, user);
            }
        }
        private void CreateCoin(long amount, User user)
        {
            user.Profile.Amount += amount;

            for (int i = 0; i < amount; i++)
            {
                Coin coin = new Coin()
                {
                    Id = BitConverter.ToInt64(Guid.NewGuid().ToByteArray()),
                    History = $"Emission to {user.Name}"
                };
                _coins.Add(coin, user);
            }
        }
        private void MoveCoins(long amount, User srcUser, User dstUser)
        {
            List<KeyValuePair<Coin, User>> coins = _coins.Where(c => c.Value.Name == srcUser.Name).Take((int) amount).ToList();

            foreach (KeyValuePair<Coin, User> coin in coins)
            {
                srcUser.Profile.Amount--;
                dstUser.Profile.Amount++;
                _coins[coin.Key] = dstUser;
                coin.Key.History += $" from {srcUser.Name} to {dstUser.Name}";
            }
        }
    }
}
