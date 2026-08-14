# Husky Visual Candidate Source Summary

- Source repository: `https://github.com/husky/husky`
- Download branch: `humble-devel`
- Download commit: `729f8aa45ccd86fa33a05e07ef698c52c451cd9c`
- Local cache: `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles/husky`
- Imported Unity subset: `Assets/VLN/ExternalAssets/HuskyVisual`
- Imported meshes: `base_link.dae`, `top_chassis.dae`, `user_rail.dae`, `bumper.dae`, `wheel.dae`
- License declaration: `husky_description/package.xml` declares `BSD`.
- Import strategy: visual mesh only; keep existing Unity ROS2 `/vln/cmd_vel`, `/tf`, camera, and LiDAR rig unchanged.

This is not a full URDF/dynamics import. It is the first safe vehicle upgrade candidate for validating a realistic UGV body in the already working Unity-ROS2 perception loop.
