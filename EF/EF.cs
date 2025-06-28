using BenchmarkDotNet.Attributes;
using Bogus;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlNoSqlPerformance.EF
{
	public class EF
	{

		public class Company
		{
			public long Id { get; set; }
			public string? Name { get; set; }
			public List<Address> Addresses { get; set; } = new List<Address>();
			public List<Tag> Tags { get; set; } = new List<Tag>();

		}
		public class Address
		{
			public long Id { get; set; }
			public string? Street { get; set; }
			public string? City { get; set; }
			public string? State { get; set; }
			public string? ZipCode { get; set; }

			public long CountryId { get; set; }
			public Country Country { get; set; } = null!; // Required for EF Core, but can be null in practice
			public long CompanyId { get; set; }
			public Company Company { get; set; } = null!; // Required for EF Core, but can be null in practice
		}
		public class Country
		{
			public long Id { get; set; }
			public string? Code { get; set; }
			public string? Name { get; set; }
		}

		public class Tag
		{
			public long Id { get; set; }

			public string? Code { get; set; }
			public string? Name { get; set; }

			public List<Company> Companies { get; } = new List<Company>();
		}

		List<Country>? countries;
		List<Tag>? tags;


		public class BlogContext : DbContext
		{
			public DbSet<Company> Companies { get; set; }

			public DbSet<Country> Countries { get; set; }

			public DbSet<Address> Addresses { get; set; }

			public DbSet<Tag> Tags { get; set; }

			protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
					=> optionsBuilder.UseNpgsql(Env.GetString("CONNECTIONSTRING"));
		}

		[GlobalSetup]
		public async Task InitializeAsync()
		{
			using var _context = new BlogContext();
			var databaseCreator = _context.GetService<IRelationalDatabaseCreator>();
			//databaseCreator.dro.CreateTables();
			_context!.Database.ExecuteSqlRaw("DROP TABLE \"CompanyTag\", \"Companies\", \"Addresses\", \"Countries\", \"Tags\"");
			databaseCreator.CreateTables();

			Faker<Country> countryFaker = new Faker<Country>()
				.RuleFor(o => o.Code, f => f.Address.CountryCode())
				.RuleFor(o => o.Name, f => f.Address.Country());

			countries = countryFaker.Generate(5);
			_context!.Countries.AddRange(countries);

			var tagFaker = new Faker<Tag>()
				.RuleFor(o => o.Code, f => f.Random.AlphaNumeric(5).ToUpper())
				.RuleFor(o => o.Name, f => f.Commerce.ProductName());
			tags = tagFaker.Generate(50);
			_context!.Tags.AddRange(tags);
			await _context!.SaveChangesAsync();
		}


		[Benchmark]
		public async Task Insert()
		{
			using var _context = new BlogContext();
			var randomAddress = new Random();
			for (int i = 0; i < 1000; i++)
			{
				Company company = CreateCompany(randomAddress);
				foreach (var tag in company.Tags)
				{
					_context.Attach(tag);
				}
				_context!.Companies.Add(company);
			}
			await _context!.SaveChangesAsync();
		}

		[Benchmark]
		public void SelectWithIncludes()
		{
			var random = new Random();
			using var _context = new BlogContext(); 
			var _ = _context!.Companies
				.Where(x => x.Id > 0)
				.Include(x => x.Addresses).ThenInclude(x => x.Country)
				.Include(x => x.Tags)
				.Take(150)
				.OrderBy(x => x.Name)
				.ToList();
			//Console.WriteLine($"Countries: {dict.Count}");
			//Console.WriteLine($"Tags: {tags.Count}");
		}

		private Company CreateCompany(Random randomAddress)
		{
			Faker<Company> faker = new Faker<Company>()
				.RuleFor(o => o.Name, f => f.Company.CompanyName());

			Faker<Address> fakerAddress = new Faker<Address>()
				.RuleFor(o => o.Street, f => f.Address.StreetAddress())
				.RuleFor(o => o.City, f => f.Address.City())
				.RuleFor(o => o.ZipCode, f => f.Address.ZipCode())
				.RuleFor(o => o.State, f => f.Address.State())
				.RuleFor(o => o.CountryId, f => f.PickRandom(countries!).Id);

			var company = faker.Generate();
			company.Addresses.AddRange(fakerAddress.GenerateBetween(1, 4));
			company.Tags.AddRange(Enumerable.Range(1, 10).Select(x => tags![randomAddress.Next(1, 50)]).Distinct().ToList());
			return company;
		}

	}
}
