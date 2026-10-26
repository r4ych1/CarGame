namespace CarGame;

internal readonly record struct VehicleInput(float Throttle, float Steering, float Brake = 0.0f);
