# Cybersickness Reduction via Gaze-Contingent Image Deformation

This repository contains an example project developed in Unity to demonstrate the method described in the paper *Cybersickness Reduction via Gaze-Contingent Image Deformation* (Groth et al., 2024). Due to copyright restrictions, the 3D environments used in the original experiment are not included. However, the example scene provided allows you to test the core functionalities of the proposed method.

For more details on the methodology, please refer to the [project page](https://graphics.tu-bs.de/publications/groth2024cybersickness) or [pdf](https://graphics.tu-bs.de/upload/publications/TOG24_Groth_sickness.pdf).

## Project Overview
The Unity project is structured as follows:

- `/Assets/Scenes/`: Contains the `DemoScene` file, which demonstrates the deformation effects that reduce cybersickness.
- `/Assets/Settings/`: Includes the forward renderer settings, which feature various `RendererFeatures` responsible for the post-processing effects. These can be toggled on and off as needed.
    - **Note:** When activating the `WarpingRendererFeature`, ensure that the `ContrastRendererFeature` is also active to maintain correct functionality.
- `/Assets/Scripts/`: Contains all relevant scripts for the renderer features, shaders, eye-tracking, and path movement.

## Requirements: Eye Tracking

This method is **gaze-contingent** and requires an eye-tracking device. The project is configured to use the **HTC VIVE PRO EYE** with **OpenXR** and the **SRanipal SDK** for real-time eye-tracking functionality. If you are using a different VR headset, adjustments to the code might be necessary.

### Setup for HTC VIVE PRO EYE:
1. Ensure that **OpenXR** and **SRanipal SDK** are correctly installed and configured.
2. Scripts in `/Assets/Scripts/` are designed to work with the SRanipal SDK. Other devices will require modifications to integrate their specific SDKs.
3. Verify that eye-tracking calibration is functional before running the demo.

### Adapting to Other Headsets:
If you are using other VR glasses with eye-tracking capabilities, you will need to:
1. Modify the code in the `/Assets/Scripts/` folder to integrate the SDK of your specific headset.
2. Adjust input mappings and eye-tracking data processing to match the format provided by your hardware.

## How to Use
1. Open the project in Unity.
2. Navigate to `/Assets/Scenes/` and load the `DemoScene`.
3. In `/Assets/Settings/`, enable or disable different `RendererFeatures` to explore the effects of the deformations.
4. Ensure that both `WarpingRendererFeature` and `ContrastRendererFeature` are active when using warping.
5. Run the scene and observe the effects of gaze-contingent deformations on the visual content.

## Citation
If you use this code, please cite our paper as follows:

```bibtex
@article{groth2024cybersickness,
  title = {Cybersickness Reduction via Gaze-Contingent Image Deformation},
  author = {Groth, Colin and Magnor, Marcus and Grogorick, Steve and Eisemann, Martin and Didyk, Piotr},
  journal = {{ACM} Transactions on Graphics (Proc. of Siggraph)},
  doi = {10.1145/3658138},
  volume = {43},
  number = {4},
  pages = {1--14},
  month = {Jul},
  year = {2024}
}
