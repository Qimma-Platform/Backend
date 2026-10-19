using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearningPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeLevelToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users', 'GradeLevel') IS NULL
BEGIN
    ALTER TABLE [Users] ADD [GradeLevel] nvarchar(max) NULL;
END;

IF COL_LENGTH('Courses', 'Description') IS NOT NULL
BEGIN
    ALTER TABLE [Courses] ALTER COLUMN [Description] nvarchar(max) NULL;
END;

IF OBJECT_ID('AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [AdminId] int NOT NULL,
        [AdminUsername] nvarchar(max) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Entity] nvarchar(max) NOT NULL,
        [Details] nvarchar(max) NOT NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF OBJECT_ID('PaymentTransactions', 'U') IS NULL
BEGIN
    CREATE TABLE [PaymentTransactions] (
        [Id] int NOT NULL IDENTITY,
        [StudentId] int NOT NULL,
        [StudentName] nvarchar(max) NOT NULL,
        [StudentEmail] nvarchar(max) NOT NULL,
        [StudentPhone] nvarchar(max) NULL,
        [CourseId] int NOT NULL,
        [CourseTitle] nvarchar(max) NOT NULL,
        [InstructorId] int NOT NULL,
        [InstructorName] nvarchar(max) NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [PaymentStatus] nvarchar(max) NOT NULL,
        [EnrolledAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([Id])
    );
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "GradeLevel",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
