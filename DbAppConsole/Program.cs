using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

using DbContextLib;
using DbModelsLib;
using DbCRUDReposLib;

namespace DbAppConsole
{
    static class MyLinqExtensions
    {
        public static void Print<T>(this IEnumerable<T> collection)
        {
            collection.ToList().ForEach(item => Console.WriteLine(item));
        }
    }
    class Program
    {
        private static DbContextOptionsBuilder<SeidoDbContext> _optionsBuilder;
        static void Main(string[] args)
        {
            if (!BuildOptions())
                return; //Terminate if not build correctly

            //Remove below comment once you have done:
            //add-migration initial-migration
            //update-database

            
            //SeedDataBase();
            QueryDatabaseAsync().Wait();
            QueryDatabase_Linq();
            QueryDatabase_DataModel_Linq();
            QueryDatabaseCRUDEAsync().Wait();

            Console.WriteLine("\nPress any key to terminate");
            Console.ReadKey();
            
        }

        private static bool BuildOptions()
        {
            _optionsBuilder = new DbContextOptionsBuilder<SeidoDbContext>();

            #region Ensuring appsettings.json is in the right location
            Console.WriteLine($"DbConnections Directory: {DBConnection.DbConnectionsDirectory}");

            var connectionString = DBConnection.ConfigurationRoot.GetConnectionString(DBConnection.ThisConnection);
            if (!string.IsNullOrEmpty(connectionString))
                Console.WriteLine($"Connection string to Database: {connectionString}");
            else
            {
                Console.WriteLine($"Please copy the 'DbConnections.json' to this location");
                return false;
            }
            #endregion

            _optionsBuilder.UseSqlite(connectionString);
            return true;
        }

        private static void SeedDataBase()
        {
            using (var db = new SeidoDbContext(_optionsBuilder.Options))
            {
                //Create some customers
                var rnd = new Random();
                for (int c = 0; c < 200; c++)
                {
                    var cus = Customer.Factory.CreateRandom();
                    
                    //Create some random orders between 0 and 50 per customer
                    for (int o = 0; o < rnd.Next(0,51); o++)
                    {
                        cus.OrdersAdd(Order.Factory.CreateRandom(cus.CustomerID));
                    }

                    db.Customers.Add(cus);
                }

                db.SaveChanges();
            }
        }
        private static async Task QueryDatabaseAsync()
        {
            Console.WriteLine("\n\nQuery Database");
            Console.WriteLine("--------------");
            using (var db = new SeidoDbContext(_optionsBuilder.Options))
            {
                var cusCount = await db.Customers.CountAsync();
                var ordCount = await db.Orders.CountAsync();

                Console.WriteLine($"Nr of Customers: {cusCount}");
                Console.WriteLine($"Nr of Orders: {ordCount}");

                var c = db.Customers.AsEnumerable();
            }
        }

        private static void QueryDatabase_Linq()
        {
            Console.WriteLine("\n\nQuery Database with Linq");
            Console.WriteLine("------------------------");
            using (var db = new SeidoDbContext(_optionsBuilder.Options))
            {
                //Use .AsEnumerable() to make sure the Db request is fully translated to be managed by Linq.
                //Use ToList() to ensure the Model is fully loaded
                var customers = db.Customers.ToList();
                var orders = db.Orders.ToList();

                Console.WriteLine($"Nr of orders: {orders.Count()}");
                Console.WriteLine($"Total order value: {orders.Sum(order => order.Total):C2}");
                
                Console.WriteLine("\nFirst 5 orders:");
                orders.Take(5).OrderByDescending(order => order.Value).Print();

                Console.WriteLine("Join examples");
                var list1 = customers.GroupJoin(orders, cust => cust.CustomerID, order => order.CustomerID, (cust, order) => new { cust, order });
                Console.WriteLine($"\nOuterJoin: Customer - Order via GroupJoin by Customer, Count: {list1.Count()}");
                //list1.Print();

                var list2 = list1.Where(custorder => custorder.order.Count() == 0);
                Console.WriteLine($"\nGroupJoin with Order list Count == 0: {list2.Count()}");
                //list2.Print();

                var list3 = list1.Where(custorder => custorder.order.Count() != 0);
                Console.WriteLine($"\nGroupJoin with Order list Count != 0: {list3.Count()}");
                //list3.Print();

                var list4 = customers.Join(orders, cust => cust.CustomerID, order => order.CustomerID, (cust, order) => new { cust, order });
                Console.WriteLine($"\nInnerJoin Customer - Order via Join, Count: {list4.Count()}");
                //list4.Print();            
            }
        }

        private static void QueryDatabase_DataModel_Linq()
        {
            Console.WriteLine("\n\nQuery Database using fully loaded datamodels");
            Console.WriteLine("------------------------");
            using (var db = new SeidoDbContext(_optionsBuilder.Options))
            {
                //Use .AsEnumerable() to make sure the Db request is fully translated to be managed by Linq.
                //Use ToList() to ensure the Model is fully loaded
                var customers = db.Customers.AsEnumerable().ToList();
                var orders = db.Orders.AsEnumerable().ToList();

                var CustomerLargestOrder = customers.OrderByDescending(c => c.OrderValue).First();
                Console.WriteLine($"Customer with largest order:\n {CustomerLargestOrder}, OrderValue: {CustomerLargestOrder.OrderValue:C2}");
           }
        }

        private static async Task QueryDatabaseCRUDEAsync()
        {
            Console.WriteLine("\n\nQuery Database CRUDE Async");
            Console.WriteLine("--------------------");
            using (var db = new SeidoDbContext(_optionsBuilder.Options))
            {
                var _repo = new CustomerRepository(db);

                Console.WriteLine("Testing ReadInfoAsync()");
                var dbInfo = await _repo.ReadDbInfoAsync();
                Console.WriteLine($"DbConnection: {dbInfo.dBConnection}");
                Console.WriteLine($"Nr of Customers: {dbInfo.NrCustomers}");
                Console.WriteLine($"Nr of Orders: {dbInfo.NrOrders}");

                Console.WriteLine("\nTesting ReadAllAsync()");
                var AllCustomers = await _repo.ReadAllAsync();
                Console.WriteLine($"Nr of Customers {AllCustomers.Count()}");
                Console.WriteLine($"\nFirst 5 Customers");
                AllCustomers.Take(5).Print();


                Console.WriteLine("\nTesting ReadAsync()");
                var LastCust1 = AllCustomers.Last();
                var LastCust2 = await _repo.ReadAsync(LastCust1.CustomerID);
                Console.WriteLine($"Last Customer with Orders.\n{LastCust1}");
                Console.WriteLine($"Read Customer with CustomerID == Last Customer\n{LastCust2}");
                if (LastCust1 == LastCust2)
                    Console.WriteLine("Customers Equal");
                else
                    Console.WriteLine("ERROR: Customers not equal");


                Console.WriteLine("\nTesting UpdateAsync()");
                LastCust2.FirstName += "_Updated";
                LastCust2.LastName += "_Updated";
                
                var LastCust3 = await _repo.UpdateAsync(LastCust2);
                Console.WriteLine($"Last Customer with updated names.\n{LastCust2}");

                if ((LastCust2.FirstName == LastCust3.FirstName) && (LastCust2.LastName == LastCust3.LastName))
                {
                    Console.WriteLine("Customer Updated");
                    LastCust3.FirstName = LastCust3.FirstName.Replace("_Updated", "");
                    LastCust3.LastName = LastCust3.LastName.Replace("_Updated", "");

                    LastCust3 = await _repo.UpdateAsync(LastCust3);
                    Console.WriteLine($"Last Customer with restored names.\n{LastCust3}");
                }
                else
                    Console.WriteLine("ERROR: Customer not updated");


                Console.WriteLine("\nTesting CreateAsync()");
                var NewCust1 = Customer.Factory.CreateRandom();
                var NewCust2 = await _repo.CreateAsync(NewCust1);
                var NewCust3 = await _repo.ReadAsync(NewCust2.CustomerID);
                
                Console.WriteLine($"Customer created.\n{NewCust1}");
                Console.WriteLine($"Customer Inserted in Db.\n{NewCust2}");
                Console.WriteLine($"Customer ReadAsync from Db.\n{NewCust3}");

                if (NewCust1 == NewCust2 && NewCust1 == NewCust3)
                    Console.WriteLine("Customers Equal");
                else
                    Console.WriteLine("ERROR: Customers not equal");


                Console.WriteLine("\nTesting DeleteAsync()");
                var DelCust1 = await _repo.DeleteAsync(NewCust1.CustomerID);
                Console.WriteLine($"Customer to delete.\n{NewCust1}");
                Console.WriteLine($"Deleted Customer.\n{DelCust1}");

                if (DelCust1 != null && DelCust1 == NewCust1)
                    Console.WriteLine("Customer Equal");
                else
                    Console.WriteLine("ERROR: Customers not equal");

                var DelCust2 = await _repo.ReadAsync(DelCust1.CustomerID);
                if (DelCust2 != null)
                    Console.WriteLine("ERROR: Customer not removed");
                else
                    Console.WriteLine("Customer confirmed removed from Db");
            }
        }
    }
}
