using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearningPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseIdToCourseTasksAndQuizAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('FK_QuizAttempts_Lessons_LessonId', 'F') IS NOT NULL
                    ALTER TABLE [QuizAttempts] DROP CONSTRAINT [FK_QuizAttempts_Lessons_LessonId];

                ALTER TABLE [QuizAttempts] ALTER COLUMN [LessonId] int NULL;

                IF COL_LENGTH('QuizAttempts', 'CourseId') IS NULL
                    ALTER TABLE [QuizAttempts] ADD [CourseId] int NULL;

                ALTER TABLE [CourseTasks] ALTER COLUMN [LessonId] int NULL;

                IF COL_LENGTH('CourseTasks', 'CourseId') IS NULL
                    ALTER TABLE [CourseTasks] ADD [CourseId] int NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QuizAttempts_CourseId' AND object_id = OBJECT_ID('QuizAttempts'))
                    CREATE INDEX [IX_QuizAttempts_CourseId] ON [QuizAttempts] ([CourseId]);

                IF OBJECT_ID('FK_QuizAttempts_Courses_CourseId', 'F') IS NULL
                    ALTER TABLE [QuizAttempts] ADD CONSTRAINT [FK_QuizAttempts_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]);

                IF OBJECT_ID('FK_QuizAttempts_Lessons_LessonId', 'F') IS NULL
                    ALTER TABLE [QuizAttempts] ADD CONSTRAINT [FK_QuizAttempts_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [Lessons] ([Id]);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuizAttempts_Courses_CourseId",
                table: "QuizAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizAttempts_Lessons_LessonId",
                table: "QuizAttempts");

            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_CourseId",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "CourseTasks");

            migrationBuilder.AlterColumn<int>(
                name: "LessonId",
                table: "QuizAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "LessonId",
                table: "CourseTasks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizAttempts_Lessons_LessonId",
                table: "QuizAttempts",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
