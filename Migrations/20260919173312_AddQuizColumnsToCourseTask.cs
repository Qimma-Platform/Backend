using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearningPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizColumnsToCourseTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseTasks_Modules_ModuleId",
                table: "CourseTasks");

            migrationBuilder.RenameColumn(
                name: "MaxScore",
                table: "CourseTasks",
                newName: "LessonId");

            migrationBuilder.AlterColumn<int>(
                name: "ModuleId",
                table: "CourseTasks",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "CorrectOptionIndex",
                table: "CourseTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "CourseTasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Options",
                table: "CourseTasks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Question",
                table: "CourseTasks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseTasks_Modules_ModuleId",
                table: "CourseTasks",
                column: "ModuleId",
                principalTable: "Modules",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseTasks_Modules_ModuleId",
                table: "CourseTasks");

            migrationBuilder.DropColumn(
                name: "CorrectOptionIndex",
                table: "CourseTasks");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "CourseTasks");

            migrationBuilder.DropColumn(
                name: "Options",
                table: "CourseTasks");

            migrationBuilder.DropColumn(
                name: "Question",
                table: "CourseTasks");

            migrationBuilder.RenameColumn(
                name: "LessonId",
                table: "CourseTasks",
                newName: "MaxScore");

            migrationBuilder.AlterColumn<int>(
                name: "ModuleId",
                table: "CourseTasks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseTasks_Modules_ModuleId",
                table: "CourseTasks",
                column: "ModuleId",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
