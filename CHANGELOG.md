# Changelog

## Program execution

The program can be executed using the following command:

`ipkcpd -h <host> -p <port> -m <mode>`

Where `host` refers to the IPv4 address of the server, `port` denotes the server port, and `mode` can be either "tcp" or "udp". The order of the flags is arbitrary, and the flags are case-insensitive. An error will be raised if `port` is not a number within the range of 0-65535.

Executing the program with the `--help` flag will display the help information.

The program will terminate with a status code of either 0 (SUCCESS) or 1 (ERROR).

## Known issues / TODO

- [ ] Verbosity flag not fully implemented.
- [ ] Checking for EOF to exit program flow.
- [ ] The evaluator also accepts more loosely defined grammar in terms of whitespace.
- [ ] Add separate evaluation of tcp queries to support negative results. 
- [ ] Write documentation for more tests.
- [ ] On Nix, when parsing a non numerical address, an exception is thrown.
