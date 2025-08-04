  _______ .______          ___   ____    ____  __  .___________.    ___           _______.
 /  _____||   _  \        /   \  \   \  /   / |  | |           |   /   \         /       |
|  |  __  |  |_)  |      /  ^  \  \   \/   /  |  | `---|  |----`  /  ^  \       |   (----`
|  | |_ | |      /      /  /_\  \  \      /   |  |     |  |      /  /_\  \       \   \    
|  |__| | |  |\  \----./  _____  \  \    /    |  |     |  |     /  _____  \  .----)   |   
 \______| | _| `._____/__/     \__\  \__/     |__|     |__|    /__/     \__\ |_______/    
                                                                                          
         .______    __    __  ____    ____  _______. __    ______     _______.            
         |   _  \  |  |  |  | \   \  /   / /       ||  |  /      |   /       |            
         |  |_)  | |  |__|  |  \   \/   / |   (----`|  | |  ,----'  |   (----`            
         |   ___/  |   __   |   \_    _/   \   \    |  | |  |        \   \                
         |  |      |  |  |  |     |  | .----)   |   |  | |  `----.----)   |               
         | _|      |__|  |__|     |__| |_______/    |__|  \______|_______/                
                                                                                          
              _______.____    ____  _______.___________. _______ .___  ___.               
             /       |\   \  /   / /       |           ||   ____||   \/   |               
            |   (----` \   \/   / |   (----`---|  |----`|  |__   |  \  /  |               
             \   \      \_    _/   \   \       |  |     |   __|  |  |\/|  |               
         .----)   |       |  | .----)   |      |  |     |  |____ |  |  |  |               
         |_______/        |__| |_______/       |__|     |_______||__|  |__|               

Full documentation available online at https://sites.google.com/view/gravitas-physics-system

----[ HOW TO USE ]----
    [Fields]:
        - Add a Gravitas field component onto a GameObject you wish to serve as a field
            * If a gravitas manager does not exist yet, this will create one in the scene
        - Ensure that a trigger collider of some sort is also attached to the GameObject
        - Assign the boundary collider field in the Gravitas field component. Usually this will be the same GameObject
        - Adjust the field settings:
            * Use the layermask to choose which layers will be considered when looking for Subjects
            * Add child colliders will add static versions of all found child colliders into the physics scene, mimicking
            the geometry of the field as it exists in the main scene
        - Adjust the gravity settings:
            * Fixed direction will override any force calculations with this direction no matter where you are in the field
            * Acceleration will control the strength and magnitude of the force
            * Distance falloff curve can be used to control how exactly distance modifies the force 
            * Priority defines how important this field is, can be overriden by fields of higher priority

    [Subjects]:
        - Add a GravitasBody component onto any RigidBody GameObject
        - Add a GravitasSubject component onto any GravitasBody GameObject that should be affected by fields
        - Adjust orientation settings if desired
            * Auto-orient will attempt to orient the subject's up axis to the force acting upon it. Useful for orbiting around planets
            * Will re-orient defines whether or not this subject will try to return to the global up axis when not being oriented
            * Orient speed naturally controls how quickly this happens
        - Assign subject colliders
            * A collection of all colliders that are a part of this subject and should be simulated. Assignable so you have complete control
        - Assign RigidBody to GravitasBody, the physics simualtion is dependant on rigidbodies, and without it will not work!

    Once both of these objects exist in a scene, fields will find and add subjects to their local physics scene for simulating.

----[ CONTROLS ]----
    [Player Demo Scene]:
        - 'W','A','S','D' = Movement
        - "Mouse" = Look
        - "Space" = Jump
        - 'E' = Interact
        - "LeftShift" = Thruster up
        - "LeftCtrl" = Thruster down
        - (In Spaceship) 'R' = Stop rotation
        - (In Spaceship) 'X' = Stop velocity
        - (In Spaceship) 'Z' / 'E' = Roll Spaceship
        - "Ctrl" + 'R' = reload scene

----[ EDITOR SETTINGS ]----
    [Gravitas Field]:
        - Boundary Collider
            * Defines the boundaries of the field that registers subject collisions
            * This should be a trigger so objects can pass through it
        - Field Layer Mask
            * The layer mask affecting which layers are involved in collisions with this field
        - Add Child colliders
            * Controls if colliders that are a child of this field are added when the physics scene is created
        - Fixed Direction (OPTIONAL)
            * Fields with a fixed direction will behave different, applying a fixed force onto all subjects regardless of distance
        - Acceleration
            * Controls the magnitude of the force vectors applied to subjects
        - Distance Falloff Curve
            * Controls how distance from the field center affects the resulting force
        - Field Priority
            * The importance of this field in relation to other fields

    [Gravitas Manager]:
        - Debug Logging Flags
            * Controls the level of debug logging during Gravitas operations, should help with debugging physics interactions
        - Physics Scene Timescale
            * Controls the ratio of time when simulating physics scenes
            * For example, a timescale of 2x will simulate physics at 2x speed compared to the main scene
            * Don't usually need to change this unless you have a use for a different timescale

    [Gravitas Subject]:
        - Auto-Orient
            * If enabled, will attempt to orient the subject to the force acting on it
        - Will Re-Orient
            * If enabled, will attempt to return the subject to the global up axis after a defined delay
        - Orient Speed
            * Controls how quickly the subject orients in both operations
        - Subject Colliders
            * A collection of colliders that should be created and considered part of this subject
            * Usually this is equivalent to all child colliders
            * Can remove or add colliders at your leisure, for more direct control on how the subject looks in physics
        - Subject Rigidbody
            * Required reference to the rigidbody driving this subject
            * Will serve as the template for physics scene subject rigidbody

----[ NOTES ]----
    Feel free to use any of the included models or utility scripts for any purpose in your own projects. The textures are also
    licensed under a CC0 license, but it's not my work so you should check out the links to get them if you want them. Let
    me know if you encounter any bugs, need any help, or if you have any feedback at awtdevcontact@gmail.com!

----[ CREDITS ]----
    - Crate Texture: https://opengameart.org/content/free-metal-texture-creation-set-03
    - Metal Panel Texture: https://opengameart.org/content/free-metal-texture-creation-set-06
    - Planet Textures: https://opengameart.org/content/planet-surface-backgrounds
    - Space Skybox Texture: https://opengameart.org/content/seamless-space-stars

    * Sunny: Emotional Support

    * Special thanks to the great work of David a.k.a PsigenVision for helping make Gravitas a better asset.
        Check him out: https://www.youtube.com/@PsigenVision
