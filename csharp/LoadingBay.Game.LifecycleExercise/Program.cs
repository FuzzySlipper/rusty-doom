PlayerInputExercise.Run();
StudyDoorExercise.Run();
RecipeAnimationExercise.Run();
NavigationGuidanceExercise.Run();
LifecycleScenario.Run((condition, message) => { if (!condition) throw new InvalidOperationException(message); });
Console.WriteLine("Loading Bay lifecycle exercise passed.");
