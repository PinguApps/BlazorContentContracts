Feature: README examples show actual compiler behaviour

    Scenario Outline: Compile the code blocks published in the README
        Given the README example '<example>'
        When the Razor compiler analyses the consumer
        Then the README diagnostics are '<diagnostics>'

        Examples:
            | example                     | diagnostics    |
            | menu items                  | none           |
            | HTML and text               | BCC005         |
            | excluded component          | BCC005         |
            | accepted component contents | none           |
            | restricted item contents    | BCC005         |
            | inline HTML                 | none           |
            | nested forbidden HTML       | BCC005         |
            | grouped items               | none           |
            | grouped violations          | BCC005         |
            | transparent wrapper         | none           |
            | invalid wrapper             | BCC005         |
            | forwarded fragments         | BCC005         |
            | branches                    | BCC005         |
            | open generics               | none           |
            | closed generics             | BCC004,BCC005  |
            | named slots                 | none           |
            | wrong named slot            | BCC005         |
            | typed template              | BCC005         |
            | forwarding promise          | none           |
            | widened forwarding promise  | BCC009         |
            | factory promise             | none           |
            | contradicted factory promise | BCC010        |
            | compiled forwarding declaration | BCC005     |
            | unknown fragment            | BCC006         |
            | mixed findings              | BCC005,BCC006  |
            | raw markup                  | BCC004         |

    @Sdk @Net10
    Scenario Outline: SDK consumers agree with the README compiler checks
        Given the README example '<example>'
        When the Razor compiler analyses the consumer
        Then the README diagnostics are '<diagnostics>'

        Examples:
            | example                     | diagnostics |
            | excluded component          | BCC005      |
            | accepted component contents | none        |
            | nested forbidden HTML       | BCC005      |
