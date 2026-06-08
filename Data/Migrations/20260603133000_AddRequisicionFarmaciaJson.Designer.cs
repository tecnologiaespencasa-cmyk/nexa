using IntranetPrueba.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260603133000_AddRequisicionFarmaciaJson")]
    partial class AddRequisicionFarmaciaJson
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "10.0.1");
#pragma warning restore 612, 618
        }
    }
}
